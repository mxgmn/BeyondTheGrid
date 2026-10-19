vec2 lambertUV(vec3 n, float angle) //n is a *unit* normal
{
    vec3 u = vec3(sin(angle), -cos(angle), 0.0);
    const float beta = 0.5; //angle in radians actually
    const float c = cos(beta);
    const float s = sin(beta);
    n = n * c + cross(u, n) * s + u * dot(u, n) * (1.0 - c);

    float r = sqrt(max(0.0, 1.0 - n.z)); //√(1−z) ∈ [0…1]
    float theta = atan(n.y, n.x) - angle + 1.5708;
    vec2 disc  = vec2(cos(theta), sin(theta)) * r;
    return disc * 0.70710678 + 0.5; //disc lies in a unit circle; fit that circle snugly into the square [0…1]², 0.7071 = √½
}

float sdRect(vec3 p, vec2 halfsize)
{
    vec2 d = abs(p.xy) - halfsize;
    return length(vec3(max(d, 0.0), p.z)) + min(max(d.x, d.y), 0.0);
}

float sdSphere(vec3 p, float radius)
{
    return length(p) - radius;
}

float sdCylinder(vec3 p, float radius, float height)
{
    vec2 d = vec2(length(p.xy) - radius, abs(p.z) - 0.5 * height);
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

float sdCuboid(vec3 p, vec3 halfsize)
{
    vec3 q = abs(p) - halfsize;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float sdCapsule(vec3 p, float radius, float height)
{
    float halfSpan = max(0.5 * height - radius, 0.0);
    p.z -= clamp(p.z, -halfSpan, halfSpan);
    return length(p) - radius;
}

mat2 rotZ(float a)
{
    float c = cos(a), s = sin(a);
    return mat2(c, -s, s, c);
}

Hit rectHit(vec3 center, float sizex, float sizey, in Ray ray)
{
    float vz = ray.vector.z;
    if(vz == 0.0) return NOHIT;

    float t = (center.z - ray.origin.z) / vz;
    if(t <= 0.0) return NOHIT;

    vec3 pHit = ray.origin + t * ray.vector;
    if (pHit.x < center.x - 0.5 * sizex || pHit.x > center.x + 0.5 * sizex || pHit.y < center.y - 0.5 * sizey || pHit.y > center.y + 0.5 * sizey) return NOHIT;

    vec3 normal = vz < 0.0 ? vec3(0,0,1) : vec3(0,0,-1);
    return Hit(t, normal, vec2(-1), 0);
}

Hit sphereHit(vec3 center, float radius, float angle, in Ray ray)
{
    vec3 tocenter = center - ray.origin;
    float b = dot(ray.vector, tocenter);
    float c = dot(tocenter, tocenter) - radius * radius;

    float D = b * b  - c;
    if (D < 0) return NOHIT;

    float t = b - sqrt(D);
    if (t <= 0) return NOHIT;

    vec3 hitPos = ray.origin + t * ray.vector;
    vec3 normal = (hitPos - center) / radius;
    return Hit(t, normal, lambertUV(normal, angle), 0);
}

Hit cylinderHit(vec3 center, float radius, float height, float angle, in Ray ray)
{
    float halfH = 0.5 * height;
    float tSide = MAXT;

    //solve quadratic for side wall in the x-y plane
    vec2 toCenter = ray.origin.xy - center.xy;
    vec2 dirXY    = ray.vector.xy;

    float a = dot(dirXY, dirXY);
    if (abs(a) >= 1e-12)
    {
        float b = dot(dirXY, toCenter);
        float c = dot(toCenter, toCenter) - radius * radius;
        float D = b*b - a*c;
        if (D >= 0.0)
        {
            float sqrtD = sqrt(D);
            float t1 = (-b - sqrtD) / a;
            float t2 = (-b + sqrtD) / a;

            if (t1 > 0.0)
            {
                float zHit1 = ray.origin.z + t1 * ray.vector.z;
                if (zHit1 >= center.z - halfH && zHit1 <= center.z + halfH)
                    tSide = t1;
            }
            if (t2 > 0.0)
            {
                float zHit2 = ray.origin.z + t2 * ray.vector.z;
                if (zHit2 >= center.z - halfH && zHit2 <= center.z + halfH && t2 < tSide)
                    tSide = t2;
            }
        }
    }

    float tCap = MAXT;
    {
        float planeZ = center.z + halfH;
        if (abs(ray.vector.z) > 1e-12)
        {
            float tHit = (planeZ - ray.origin.z) / ray.vector.z;
            if (tHit > 0.0)
            {
                vec3 pHit = ray.origin + tHit * ray.vector;
                vec2 dxy  = pHit.xy - center.xy;
                if (dot(dxy, dxy) <= radius * radius)
                    tCap = tHit;
            }
        }
    }

    float tMin = min(tSide, tCap);
    if (tMin == MAXT) return NOHIT;
    else if (tMin == tCap)
    {
        vec3 hitPos = ray.origin + tMin * ray.vector;
        vec3 pLocal = hitPos - center;
        vec2 xyRot = rotZ(angle - 1.5708) * pLocal.xy;
        vec2 uv = 1.25 * (xyRot / vec2(2.0 * radius)) + vec2(0.5);
        return Hit(tMin, vec3(0, 0, 1), uv, 0);
    }
    else
    {
        vec3 pHit = ray.origin + tMin * ray.vector;
        vec2 nxy  = (pHit.xy - center.xy) / radius;
        vec3 normal = vec3(nxy.x, nxy.y, 0.0);
        return Hit(tMin, normal, vec2(-1), 0);
    }
}

Hit cuboidHit(vec3 center, vec3 halfsize, in Ray ray)
{
    vec3 tMin = (center - halfsize - ray.origin) / ray.vector;
    vec3 tMax = (center + halfsize - ray.origin) / ray.vector;

    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);

    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);

    if (tNear > tFar || tFar < 0.0) return NOHIT;

    float tHit = (tNear > 0.0) ? tNear : tFar;
    vec3 hitPos = ray.origin + tHit * ray.vector;
    vec3 normal = vec3(1.0);
    vec2 uv = vec2(-1.0);

    if (abs(hitPos.x - (center.x - halfsize.x)) < 0.001) normal = vec3(-1, 0, 0);
    else if (abs(hitPos.x - (center.x + halfsize.x)) < 0.001) normal = vec3(1, 0, 0);
    else if (abs(hitPos.y - (center.y - halfsize.y)) < 0.001) normal = vec3(0, -1, 0);
    else if (abs(hitPos.y - (center.y + halfsize.y)) < 0.001) normal = vec3(0, 1, 0);
    else if (abs(hitPos.z - (center.z - halfsize.z)) < 0.001) normal = vec3(0, 0, -1);
    else if (abs(hitPos.z - (center.z + halfsize.z)) < 0.001)
    {
        normal = vec3(0, 0, 1);
        vec3 pLocal = hitPos - center;
        uv = 1.1 * (pLocal.xy / (2.0 * halfsize.xy)) + vec2(0.5);
    }

    return Hit(tHit, normal, uv, 0);
}

Hit myCylinderHit(vec3 center, float radius, float height, in Ray ray)
{
    vec2 tocenter = ray.origin.xy - center.xy;
    float a = dot(ray.vector.xy, ray.vector.xy);
    float b = dot(ray.vector.xy, tocenter);
    float c = dot(tocenter, tocenter) - radius * radius;

    float D = b * b - a * c;
    if (D < 0) return NOHIT;

    float t = (-b - sqrt(D)) / a;
    if (t <= 0) return NOHIT;
    if (abs(ray.origin.z + t * ray.vector.z - center.z) > 0.5 * height) return NOHIT;

    vec2 nxy = (tocenter + t * ray.vector.xy) / radius;
    return Hit(t, vec3(nxy.x, nxy.y, 0.0), vec2(-1), 0);
}

Hit capsuleHit(vec3 center, float radius, float height, float angle, in Ray ray)
{
    Hit argmin;
    argmin.t = MAXT;

    Hit cylhit = myCylinderHit(center, radius, height - 2.0 * radius, ray);
    if (cylhit.t < argmin.t) argmin = cylhit;

    float ztop = center.z + 0.5 * height - radius;
    Hit tophit = sphereHit(vec3(center.x, center.y, ztop), radius, angle, ray);
    if (tophit.t < argmin.t) argmin = tophit;

    float zbot = center.z - 0.5 * height + radius;
    Hit bothit = sphereHit(vec3(center.x, center.y, zbot), radius, angle, ray);
    bothit.uv = vec2(-1);
    if (bothit.t < argmin.t) argmin = bothit;

    return argmin;
}
