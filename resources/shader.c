#version 330
out vec4 finalColor;

uniform vec2 resolution; //window size in pixels
uniform vec3 cameraPosition, cameraForward, cameraCorner, cameraHorizontal, cameraVertical;

uniform int numObjects, numTextures;
uniform int cameraOrthographic;
uniform sampler2D data;
uniform sampler2D voxels;
uniform sampler2D emojiTex;
uniform sampler2D voxelFaceTex;
uniform vec3 VM;
uniform vec3 toSun;
uniform float voxelSize;

const float PI = 3.1415926536;
const float MAXT = 1000000.0;
const int VOXEL_CODE = -3;
const int TEXELS_PER_OBJECT = 3;
const float CLIP_NEAR = 0.01;
const float CLIP_FAR = 1000.0;
const bool ENABLE_SSAA_2X2 = false;

const ivec2 VOXEL_ATLAS_GRID = ivec2(10, 2);
const int NUM_VOXEL_TYPES = 5; //TODO: нельзя ли как-нибудь от этого избавиться? Наверняка можно
const ivec2 VOXEL_TEXTURES[NUM_VOXEL_TYPES] = ivec2[]( //наверное нужно просто удвоить этот массив
    ivec2(255, 255),
    ivec2(0, 0),
    ivec2(1, 11),
    ivec2(2, 2),
    ivec2(12, 12)
);
const vec3 SUNDIR[VOXEL_ATLAS_GRID.x * VOXEL_ATLAS_GRID.y] = vec3[](
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(1, 1, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(1, 1, 1),
    vec3(0, 0, 1),
    vec3(0, 1, 1),

    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1),
    vec3(1, 1, 1),
    vec3(0, 0, 1),
    vec3(0, 0, 1)
);

struct Ray
{
    vec3 origin;
    vec3 vector;
};

struct Hit
{
    float t;
    vec3 normal;
    vec2 uv;
    int voxelType;
};

struct RenderResult
{
    vec3 color;
    float depth;
};

const Hit NOHIT = Hit(MAXT, vec3(1,1,1), vec2(-1,-1), 0);

vec3 voxelGridMin()
{
    return vec3(-0.5 * VM.xy * voxelSize, -2.0 * voxelSize);
}

int voxelIndex(ivec3 cell)
{
    ivec3 vm = ivec3(VM);
    if (any(lessThan(cell, ivec3(0))) || any(greaterThanEqual(cell, vm))) return -1;
    return cell.x + vm.x * (cell.y + vm.y * cell.z);
}

int voxelType(ivec3 cell)
{
    int index = voxelIndex(cell);
    return index < 0 ? 0 : int(round(255.0 * texelFetch(voxels, ivec2(index, 0), 0).r));
}

bool clipBox(in Ray ray, vec3 boxMin, vec3 boxMax, out float tEnter, out float tExit, out vec3 enterNormal, out vec3 exitNormal)
{
    tEnter = -MAXT;
    tExit = MAXT;
    enterNormal = vec3(0.0);
    exitNormal = vec3(0.0);
    for (int axis = 0; axis < 3; ++axis)
    {
        float direction = ray.vector[axis];
        if (abs(direction) < 1e-6)
        {
            if (ray.origin[axis] < boxMin[axis] || ray.origin[axis] > boxMax[axis]) return false;
            continue;
        }
        float a = (boxMin[axis] - ray.origin[axis]) / direction;
        float b = (boxMax[axis] - ray.origin[axis]) / direction;
        vec3 normal = vec3(0.0);
        normal[axis] = direction > 0.0 ? -1.0 : 1.0;
        if (min(a, b) > tEnter)
        {
            tEnter = min(a, b);
            enterNormal = normal;
        }
        if (max(a, b) < tExit)
        {
            tExit = max(a, b);
            exitNormal = -normal;
        }
        if (tEnter > tExit) return false;
    }
    return true;
}

bool voxelCellHit(in Ray ray, vec3 gridMin, ivec3 cell, float tMinAllowed, float tMaxAllowed, out float hitT, out vec3 hitNormal)
{
    vec3 cellMin = gridMin + voxelSize * vec3(cell);
    vec3 localOrigin = (ray.origin - cellMin) / voxelSize;
    vec3 localDir = ray.vector / voxelSize;

    float tEnter, tExit;
    vec3 enterNormal, exitNormal;
    if (!clipBox(Ray(localOrigin, localDir), vec3(0.0), vec3(1.0), tEnter, tExit, enterNormal, exitNormal)) return false;

    if (tExit < max(tMinAllowed, 0.0) || tEnter > tMaxAllowed) return false;

    if (tEnter >= max(tMinAllowed, 0.0) - 1e-5)
    {
        hitT = tEnter;
        hitNormal = enterNormal;
        return hitT <= tMaxAllowed + 1e-5;
    }

    if (tExit >= max(tMinAllowed, 0.0) - 1e-5)
    {
        hitT = tExit;
        hitNormal = exitNormal;
        return hitT <= tMaxAllowed + 1e-5;
    }

    return false;
}

Hit voxelHit(in Ray ray)
{
    ivec3 vm = ivec3(VM);
    vec3 gridMin = voxelGridMin();
    vec3 gridMax = gridMin + voxelSize * VM;

    float tEnter, tExit;
    vec3 enterNormal, exitNormal;
    if (!clipBox(ray, gridMin, gridMax, tEnter, tExit, enterNormal, exitNormal) || tExit <= 0.0) return NOHIT;

    float t = max(tEnter, 0.0);
    vec3 p = ray.origin + t * ray.vector;
    vec3 local = (p - gridMin) / voxelSize;
    ivec3 cell = ivec3(clamp(floor(local), vec3(0.0), vec3(vm) - vec3(1.0)));

    vec3 tMax = vec3(MAXT);
    vec3 tDelta = vec3(MAXT);
    ivec3 step = ivec3(sign(ray.vector));
    int totalVoxels = vm.x * vm.y * vm.z;

    if (ray.vector.x > 0.0)
    {
        tMax.x = (gridMin.x + (float(cell.x) + 1.0) * voxelSize - ray.origin.x) / ray.vector.x;
        tDelta.x = voxelSize / ray.vector.x;
    }
    else if (ray.vector.x < 0.0)
    {
        tMax.x = (gridMin.x + float(cell.x) * voxelSize - ray.origin.x) / ray.vector.x;
        tDelta.x = -voxelSize / ray.vector.x;
    }

    if (ray.vector.y > 0.0)
    {
        tMax.y = (gridMin.y + (float(cell.y) + 1.0) * voxelSize - ray.origin.y) / ray.vector.y;
        tDelta.y = voxelSize / ray.vector.y;
    }
    else if (ray.vector.y < 0.0)
    {
        tMax.y = (gridMin.y + float(cell.y) * voxelSize - ray.origin.y) / ray.vector.y;
        tDelta.y = -voxelSize / ray.vector.y;
    }

    if (ray.vector.z > 0.0)
    {
        tMax.z = (gridMin.z + (float(cell.z) + 1.0) * voxelSize - ray.origin.z) / ray.vector.z;
        tDelta.z = voxelSize / ray.vector.z;
    }
    else if (ray.vector.z < 0.0)
    {
        tMax.z = (gridMin.z + float(cell.z) * voxelSize - ray.origin.z) / ray.vector.z;
        tDelta.z = -voxelSize / ray.vector.z;
    }

    for (int stepCount = 0; stepCount < totalVoxels; ++stepCount)
    {
        float cellExit = min(tExit, min(tMax.x, min(tMax.y, tMax.z)));
        int type = voxelType(cell);
        if (type > 0)
        {
            float hitT;
            vec3 hitNormal;
            if (voxelCellHit(ray, gridMin, cell, max(t, 0.0), cellExit, hitT, hitNormal)) return Hit(hitT, hitNormal, vec2(-1), type);
        }

        if (tMax.x <= tMax.y && tMax.x <= tMax.z)
        {
            t = tMax.x;
            if (t > tExit) break;
            cell.x += step.x;
            tMax.x += tDelta.x;
            if (cell.x < 0 || cell.x >= vm.x) break;
        }
        else if (tMax.y <= tMax.z)
        {
            t = tMax.y;
            if (t > tExit) break;
            cell.y += step.y;
            tMax.y += tDelta.y;
            if (cell.y < 0 || cell.y >= vm.y) break;
        }
        else
        {
            t = tMax.z;
            if (t > tExit) break;
            cell.z += step.z;
            tMax.z += tDelta.z;
            if (cell.z < 0 || cell.z >= vm.z) break;
        }
    }

    return NOHIT;
}

#include "rayIntersect.c"

Hit firstHit(in Ray ray, out int hitIndex, bool anyHit) //anyHit=true enables early exit
{
    Hit argmin = NOHIT;
    hitIndex = -1;

    Hit voxhit = voxelHit(ray);
    if (voxhit.t < argmin.t)
    {
        argmin = voxhit;
        hitIndex = VOXEL_CODE;
        if (anyHit) return argmin;
    }

    for (int i = 0; i < numObjects; i++)
    {
        int base = i * TEXELS_PER_OBJECT;
        vec4 p0 = texelFetch(data, ivec2(base + 0,0), 0); //size
        vec4 p1 = texelFetch(data, ivec2(base + 1,0), 0); //center
        vec3 size = p0.rgb;
        float shape = p0.a;
        vec3 center = p1.rgb;
        float angle = p1.a;

        Hit hit = NOHIT;
        if (shape == 0) hit = rectHit(center, size.x, size.y, ray);
        else if (shape == 1) hit = sphereHit(center, (size.x + size.y + size.z) / 6.0, angle, ray);
        else if (shape == 2) hit = cylinderHit(center, 0.25 * (size.x + size.y), size.z, angle, ray);
        else if (shape == 3) hit = cuboidHit(center, 0.5 * size, ray);
        else if (shape == 4) hit = capsuleHit(center, 0.25 * (size.x + size.y), size.z, angle, ray);

        if (hit.t < argmin.t)
        {
            argmin = hit;
            hitIndex = i;
            if (anyHit) return argmin;
        }
    }
    return argmin;
}

vec2 voxelFaceUV(vec3 position, vec3 normal)
{
    vec3 sample = (position - voxelGridMin()) / voxelSize - 0.001 * normal;

    if (abs(normal.z) > 0.5) return fract(sample.xy);
    if (abs(normal.x) > 0.5)
    {
        float u = normal.x > 0.0 ? 1.0 - fract(sample.y) : fract(sample.y);
        return vec2(u, 1.0 - fract(sample.z));
    }

    float u = normal.y > 0.0 ? fract(sample.x) : 1.0 - fract(sample.x);
    return vec2(u, 1.0 - fract(sample.z));
}

void voxelFaceBasis(vec3 normal, out vec3 uAxis, out vec3 vAxis)
{
    if (abs(normal.z) > 0.5)
    {
        uAxis = vec3(1.0, 0.0, 0.0);
        vAxis = vec3(0.0, 1.0, 0.0);
    }
    else if (abs(normal.x) > 0.5)
    {
        uAxis = normal.x > 0.0 ? vec3(0.0, -1.0, 0.0) : vec3(0.0, 1.0, 0.0);
        vAxis = vec3(0.0, 0.0, -1.0);
    }
    else
    {
        uAxis = normal.y > 0.0 ? vec3(1.0, 0.0, 0.0) : vec3(-1.0, 0.0, 0.0);
        vAxis = vec3(0.0, 0.0, -1.0);
    }
}

vec2 rotateCW(vec2 v, int turns)
{
    if (turns == 1) return vec2(v.y, -v.x);
    if (turns == 2) return -v;
    if (turns == 3) return vec2(-v.y, v.x);
    return v;
}

vec2 rotateTextureUV(vec2 uv, int turns)
{
    if (turns == 1) return vec2(1.0 - uv.y, uv.x);
    if (turns == 2) return vec2(1.0 - uv.x, 1.0 - uv.y);
    if (turns == 3) return vec2(uv.y, 1.0 - uv.x);
    return uv;
}

int voxelTextureRotation(int textureIndex, vec3 normal)
{
    int tileCount = VOXEL_ATLAS_GRID.x * VOXEL_ATLAS_GRID.y;
    if (textureIndex < 0 || textureIndex >= tileCount) return 0;

    vec3 artistSun = SUNDIR[textureIndex];
    artistSun.y = -artistSun.y;
    vec3 uAxis, vAxis;
    voxelFaceBasis(normal, uAxis, vAxis);
    vec3 sceneSun = vec3(dot(toSun, uAxis), dot(toSun, vAxis), dot(toSun, normal));
    //sceneSun.xy = rotateCW(sceneSun.xy, 1);

    int bestTurns = 0;
    float bestScore = dot(sceneSun, artistSun);
    for (int turns = 1; turns < 4; ++turns)
    {
        float score = dot(sceneSun, vec3(rotateCW(artistSun.xy, turns), artistSun.z));
        if (score > bestScore)
        {
            bestScore = score;
            bestTurns = turns;
        }
    }
    return bestTurns;
}

vec3 sampleVoxelAtlas(int textureIndex, vec2 uv)
{
    int tileCount = VOXEL_ATLAS_GRID.x * VOXEL_ATLAS_GRID.y;
    if (textureIndex < 0 || textureIndex >= tileCount) return vec3(1.0, 0.0, 1.0);
    ivec2 tile = ivec2(textureIndex % VOXEL_ATLAS_GRID.x, textureIndex / VOXEL_ATLAS_GRID.x);
    
    return texture(voxelFaceTex, (vec2(tile) + uv) / vec2(VOXEL_ATLAS_GRID)).rgb;
}

vec3 voxelColor(vec3 position, vec3 normal, int type)
{
    vec2 uv = voxelFaceUV(position, normal);
    if (type <= 0 || type >= NUM_VOXEL_TYPES) return vec3(1.0, 0.0, 1.0);

    int textureIndex = abs(normal.z) > 0.5 ? VOXEL_TEXTURES[type].x : VOXEL_TEXTURES[type].y;
    return sampleVoxelAtlas(textureIndex, rotateTextureUV(uv, voxelTextureRotation(textureIndex, normal)));
}

float sceneDepth(vec3 worldPosition)
{
    float distanceAlongView = dot(worldPosition - cameraPosition, cameraForward);
    if (cameraOrthographic != 0) return clamp((distanceAlongView - CLIP_NEAR) / (CLIP_FAR - CLIP_NEAR), 0.0, 1.0);

    float ndcZ = (CLIP_FAR + CLIP_NEAR) / (CLIP_FAR - CLIP_NEAR) - (2.0 * CLIP_FAR * CLIP_NEAR) / ((CLIP_FAR - CLIP_NEAR) * distanceAlongView);
    return clamp(0.5 * ndcZ + 0.5, 0.0, 1.0);
}

float voxelDistance(vec3 position)
{
    vec3 gridMin = voxelGridMin();
    ivec3 baseCell = ivec3(floor((position - gridMin) / voxelSize));
    float dist = MAXT;

    for (int z = -1; z <= 1; ++z)
    {
        for (int y = -1; y <= 1; ++y)
        {
            for (int x = -1; x <= 1; ++x)
            {
                ivec3 cell = baseCell + ivec3(x, y, z);
                if (voxelType(cell) <= 0) continue;

                vec3 cellMin = gridMin + voxelSize * vec3(cell);
                vec3 local = (position - cellMin) / voxelSize;
                dist = min(dist, voxelSize * sdCuboid(local - vec3(0.5), vec3(0.5)));
            }
        }
    }
    return dist;
}

float primitiveDistance(vec3 position)
{
    float dist = MAXT;
    for (int i = 0; i < numObjects; ++i)
    {
        int base = i * TEXELS_PER_OBJECT;
        vec4 p0 = texelFetch(data, ivec2(base + 0,0), 0);
        vec4 p1 = texelFetch(data, ivec2(base + 1,0), 0);
        vec3 size = p0.rgb;
        float shape = p0.a;
        vec3 local = position - p1.rgb;

        if (shape == 0) dist = min(dist, max(sdRect(local, 0.5 * size.xy), 0.0));
        else if (shape == 1) dist = min(dist, max(sdSphere(local, (size.x + size.y + size.z) / 6.0), 0.0));
        else if (shape == 2) dist = min(dist, max(sdCylinder(local, 0.25 * (size.x + size.y), size.z), 0.0));
        else if (shape == 3) dist = min(dist, max(sdCuboid(local, 0.5 * size), 0.0));
        else if (shape == 4) dist = min(dist, max(sdCapsule(local, 0.25 * (size.x + size.y), size.z), 0.0));
    }
    return dist;
}

float sceneDistance(vec3 position)
{
    return min(primitiveDistance(position), voxelDistance(position));
}

float ambientOcclusion(vec3 position, vec3 normal)
{
    float occ = 0.0;
    float sca = 1.0;
    for (int i = 0; i < 3; ++i)
    {
        float h = voxelSize * (0.08 + 0.08 * float(i));
        float d = sceneDistance(position + h * normal);
        occ += max(h - d, 0.0) * sca;
        sca *= 0.5;
    }
    return clamp(1.0 - 5.0 * occ / voxelSize, 0.0, 1.0);
}

RenderResult render(in Ray ray)
{
    float sunIntensity = 0.5;
    float skyIntensity = 1.0 - sunIntensity;

    int hitIndex;
    Hit hit = firstHit(ray, hitIndex, false);

    if (hit.t < MAXT)
    {
        float cosinus = max(dot(hit.normal, toSun), 0.0);
        vec3 p = ray.origin + hit.t * ray.vector; //в этой точке мы захитили
        int shadowIndex = -1;

        vec3 albedo = vec3(1,0,1);
        if (hitIndex == VOXEL_CODE) albedo = voxelColor(p, hit.normal, hit.voxelType);
        else
        {
            int baseIndex = hitIndex * TEXELS_PER_OBJECT;
            vec4 p2 = texelFetch(data, ivec2(baseIndex + 2,0), 0); //color

            vec3 color = p2.rgb;

            vec2 uv = hit.uv;
            float textureIndex = p2.a;
            if (textureIndex >= 0 && numTextures > 0)
            {
                uv.x = (uv.x + textureIndex) / numTextures;
                if (uv.x >= textureIndex / numTextures && uv.x <= (textureIndex + 1.0) / numTextures && uv.y >= 0 && uv.y <= 1)
                {
                    vec4 tex = texture(emojiTex, uv);

                    const float tintStrength = 0.1;
                    vec3 texFinal = mix(tex.rgb, color * tex.rgb, tintStrength);
                    albedo = mix(color, texFinal, tex.a);
                }
                else albedo = color;
            }
            else albedo = color;
        }
        if (firstHit(Ray(p + 0.001 * hit.normal, toSun), shadowIndex, true).t < MAXT) cosinus *= 0.125; //*= 0.0 for complete shadow
        float ao = ambientOcclusion(p, hit.normal);
        float ambient = 0.25 + 0.75 * ao;
        float light = cosinus * sunIntensity + ambient * skyIntensity;
        vec3 result = light * albedo;
        float fog = 1.0 - smoothstep(-2.0, -0.4, p.z); //0.1
        result = mix(result, vec3(1.0), fog);
        return RenderResult(result, sceneDepth(p));
    }
    else return RenderResult(vec3(1.0), 1.0);
}

RenderResult renderPixel(vec2 fragCoord)
{
    vec2 uv = fragCoord / resolution;
    vec3 rayOrigin = cameraCorner + uv.x * cameraHorizontal + uv.y * cameraVertical;
    vec3 cameraVector = cameraOrthographic != 0 ? cameraForward : normalize(rayOrigin - cameraPosition);
    return render(Ray(cameraOrthographic != 0 ? rayOrigin : cameraPosition, cameraVector));
}

void main()
{
    RenderResult result = renderPixel(gl_FragCoord.xy);
    if (ENABLE_SSAA_2X2)
    {
        vec2 p = gl_FragCoord.xy;
        result.color = 0.25 * (
            renderPixel(p + vec2(-0.25, -0.25)).color +
            renderPixel(p + vec2( 0.25, -0.25)).color +
            renderPixel(p + vec2(-0.25,  0.25)).color +
            renderPixel(p + vec2( 0.25,  0.25)).color
        );
    }
    vec3 color = pow(result.color, vec3(0.8)); //0.7 -> 0.85
    finalColor = vec4(color, 1.0);
    gl_FragDepth = min(result.depth, 1.0 - 1e-7);
}
