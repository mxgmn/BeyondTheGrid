using System.Numerics;

struct Codon
{
    public OP op;
    public int opvariant;

    public Codon(OP op, int opvariant)
    {
        this.op = op;
        this.opvariant = opvariant;
    }
    public Codon(OP op)
    {
        this.op = op;
        opvariant = -1;
    }

    public readonly override string ToString()
    {
        if (op == OP.DBVAR) return $"#{opvariant}";
        else if (op == OP.EXISTS || op == OP.CLOSEST || op == OP.FOREACH || op == OP.MAX_DIRECTION || op == OP.AVERAGE_DIRECTION || op == OP.MAX_TILE) return $"{Record.dict[op].name} {opvariant}";
        else if (op == OP.RETURN) return "RETURN";
        else if (op == OP.DECREMENT) return "DEC";
        else if (op == OP.POP) return "POP";
        else if (op == OP.DUP) return "DUP";
        else if (op == OP.JUMP) return $"JUMP{opvariant:+0;-0;+0}";
        else if (op == OP.JUMP_IF_FALSE_B) return $"JUMP_IF_FALSE_B{opvariant:+0;-0;+0}";
        else if (op == OP.JUMP_IF_FALSE_F) return $"JUMP_IF_FALSE_F{opvariant:+0;-0;+0}";
        else if (op == OP.FLOAT) return $"FLOAT-{opvariant}";
        else if (op == OP.STRING) return $"STRING-{opvariant}";
        else if (op == OP.TEMPLATE) return $"TEMPLATE-{opvariant}";
        else if (op == OP.LOAD) return $"LOAD-{opvariant}";
        else if (op == OP.STORE) return $"STORE-{opvariant}";
        else if (op == OP.TILE_VALUE) return $"TILE-{opvariant}";
        else return Record.dict[op].name;
    }
}

class Script : Error
{
    static readonly Vector2[] directionRotations = Enumerable.Range(0, Settings.NUM_RAYS).Select(n => VH.Unit((n % 2 == 0 ? 1 : -1) * (n + 1) * (Settings.MAX_DIRECTION_ZONE * MathF.Tau / Settings.NUM_RAYS))).ToArray();

    public Codon[] code;    
    public float[] floats;
    public string[] strings;
    public TYPE type;

    public Script(List<Codon> code, List<float> floats, List<string> strings, TYPE type)
    {
        if (code != null)
        {
            this.code = code.ToArray();
            this.floats = floats.ToArray();
            this.strings = strings.ToArray();
        }
        this.type = type;
    }

    public override string ToString()
    {
        string result = "";
        for (int i = 0; i < code.Length; i++) result += code[i] + " ";

        if (floats != null) result += "," + Environment.NewLine + "floats = [" + string.Join(", ", floats) + "],";
        if (strings != null) result += Environment.NewLine + "strings = [" + string.Join(", ", strings) + "]";
        return result;
    }

    public Value Execute(State state, int selfID, Value[] storage, ReadOnlySpan<Value> newInputs, int start = 0, MyStack stack = null)
    {
        if (stack == null) stack = state.stack;
        int stackStart = stack.numItems;
        for (int i = 0; i < newInputs.Length; i++) state.inputs.Push(newInputs[i]);

        for (int indx = start; indx < code.Length;)
        {
            OP op = code[indx].op;
            int opvariant = code[indx++].opvariant;

            switch (op)
            {
                case OP.DBVAR: stack.Push(state.inputs.data[opvariant]); break;
                case OP.RETURN: indx = code.Length; break;
                case OP.STORE: storage[opvariant] = stack.Pop(); break;
                case OP.DECREMENT: stack.Push(Value.FloatValue(stack.Pop().v.X - 1f)); break;
                case OP.POP: stack.Pop(); break;
                case OP.DUP: stack.Push(stack.Peek()); break;
                case OP.JUMP: indx += opvariant - 1; break; // offsets are relative to the instruction, before indx++
                case OP.JUMP_IF_FALSE_B:
                    if (stack.Pop().i <= 0) indx += opvariant - 1;
                    break;
                case OP.JUMP_IF_FALSE_F:
                    if (stack.Pop().v.X == 0f) indx += opvariant - 1;
                    break;
                case OP.FLOAT: stack.Push(Value.FloatValue(floats[opvariant])); break;
                case OP.TILE_VALUE: stack.Push(Value.TileValue((byte)opvariant)); break;
                case OP.STRING: stack.Push(Value.StringValue(opvariant)); break;
                case OP.TEMPLATE: stack.Push(Value.TemplateValue(opvariant)); break;
                case OP.LOAD: stack.Push(storage[opvariant]); break;
                case OP.PASS: stack.Push(Value.PASS); break;
                case OP.TURN: stack.Push(Value.TupleValue(0, false, stack.Pop().v)); break;
                case OP.BACK: stack.Push(Value.TupleValue(-1, false, Vector2.Zero)); break;
                case OP.FALSE: stack.Push(Value.FALSE); break;
                case OP.TRUE: stack.Push(Value.TRUE); break;
                case OP.NOT: stack.Push(stack.Pop().i > 0 ? Value.FALSE : Value.TRUE); break;
                case OP.EEQ_T:
                case OP.EEQ_O: stack.Push(stack.Pop().i == stack.Pop().i ? Value.TRUE : Value.FALSE); break;
                case OP.EEQ_F: stack.Push(stack.Pop().v.X == stack.Pop().v.X ? Value.TRUE : Value.FALSE); break;
                case OP.LESS: stack.Push(stack.Pop().v.X > stack.Pop().v.X ? Value.TRUE : Value.FALSE); break;
                case OP.LESSEQ: stack.Push(stack.Pop().v.X >= stack.Pop().v.X ? Value.TRUE : Value.FALSE); break;
                case OP.ALIGNED: stack.Push(Vector2.Dot(stack.Pop().v, stack.Pop().v) > 0f ? Value.TRUE : Value.FALSE); break;
                case OP.DOTPRODUCT: stack.Push(Value.FloatValue(Vector2.Dot(stack.Pop().v, stack.Pop().v))); break;
                case OP.OPP_F: stack.Push(Value.FloatValue(-stack.Pop().v.X)); break;
                case OP.PLUS_FF: stack.Push(Value.FloatValue(stack.Pop().v.X + stack.Pop().v.X)); break;
                case OP.MINUS_FF: stack.Push(Value.FloatValue(-stack.Pop().v.X + stack.Pop().v.X)); break;
                case OP.MULT_FF: stack.Push(Value.FloatValue(stack.Pop().v.X * stack.Pop().v.X)); break; ///можно считать, что все мультиумножения конвертируются в бинарные умножения на этапе трансляции
                case OP.LENGTH: stack.Push(Value.FloatValue(stack.Pop().v.Length())); break;
                case OP.CONSTANT: stack.Push(Value.FloatValue(Game.constant)); break;
                case OP.X2_F: stack.Push(Value.FloatValue(2f * stack.Pop().v.X)); break;
                case OP.SELF: stack.Push(Value.ObjectValue(selfID)); break;
                case OP.NONE: stack.Push(Value.NONE); break;
                case OP.LEFT: stack.Push(Value.VectorValue(stack.Pop().v.Left())); break;
                case OP.RIGHT: stack.Push(Value.VectorValue(stack.Pop().v.Right())); break;
                case OP.X2_V: stack.Push(Value.VectorValue(2f * stack.Pop().v)); break;
                case OP.PLUS_VV: stack.Push(Value.VectorValue(stack.Pop().v + stack.Pop().v)); break;
                case OP.MINUS_VV: stack.Push(Value.VectorValue(-stack.Pop().v + stack.Pop().v)); break;
                case OP.MULT_FV: stack.Push(Value.VectorValue(stack.Pop().v * stack.Pop().v.X)); break;
                case OP.NOTHING: stack.Push(Value.NOTHING); break;
                case OP.ALL: stack.Push(Value.ALL); break;
                case OP.ALL_VISIBLE: stack.Push(Value.ALL_VISIBLE); break;
                case OP.EXISTS:
                    {
                        bool onlyVisible = stack.Pop().i > 0; ///GetList();
                        Object self = state.array[selfID];

                        bool exists = false;
                        for (int i = 0; i < state.maxObjectCount; i++)
                        {
                            Object o = state.array[i];
                            if (o == null || i == selfID) continue;
                            if (onlyVisible && !Graphics.Sees(self, o, state)) continue;

                            Value v = Execute(state, selfID, storage, [Value.ObjectValue(i)], opvariant);
                            if (v.i > 0)
                            {
                                exists = true;
                                break;
                            }
                        }
                        stack.Push(exists ? Value.TRUE : Value.FALSE);
                        break;
                    }
                case OP.CLOSEST:
                    {
                        bool onlyVisible = stack.Pop().i > 0; ///GetList();
                        Object self = stack.Pop().GetObject(state);

                        int argmin = -1;
                        float minDistance = 1000f;
                        for (int i = 0; i < state.maxObjectCount; i++)
                        {
                            Object o = state.array[i];
                            if (o == null || o == self || o.holderUID >= 0) continue;
                            if (onlyVisible && !Graphics.Sees(self, o, state)) continue;

                            Value v = Execute(state, selfID, storage, [Value.ObjectValue(i)], opvariant);
                            if (v.i == 0) continue; ///if (!v.GetBool()) continue;

                            float distance = Body.BodyDistance2D(self.body, o.body);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                argmin = i;
                            }
                        }
                        stack.Push(Value.ObjectValue(argmin));
                        break;
                    }
                case OP.MAX_DIRECTION:
                    {
                        float max = -1e+6f;
                        Vector2 argmax = Vector2.Zero;
                        Vector2 v = state.array[selfID].dir;

                        for (int n = 0; n < Settings.NUM_RAYS / 1; n++)
                        {
                            float f = Execute(state, selfID, storage, [Value.VectorValue(v)], opvariant).v.X;
                            if (f > max)
                            {
                                max = f;
                                argmax = v;
                            }
                            Vector2 rotation = directionRotations[n];
                            v = new Vector2(v.X * rotation.X - v.Y * rotation.Y, v.X * rotation.Y + v.Y * rotation.X);
                        }
                        stack.Push(Value.VectorValue(max * argmax));
                        break;
                    }
                case OP.AVERAGE_DIRECTION:
                    {
                        Object self = state.array[selfID];
                        const float POWER = 1.5f;
                        const int HALFRAYS = 3;
                        Vector2 v, sum = Vector2.Zero;
                        for (int i = -HALFRAYS; i <= HALFRAYS; i++) //потом перейти на количество лучей и ширину конуса
                        {
                            float angle = 0.95f * i * MathF.PI / 6f;
                            angle += 0.25f * (state.lcg.NextSingle() - 0.5f);
                            v = self.dir.Rotated(angle);

                            float f = Execute(state, selfID, storage, [Value.VectorValue(v)], opvariant).v.X;
                            sum += MathF.Pow(f, POWER) * v;
                        }

                        sum /= (2 * HALFRAYS + 1);
                        float length = sum.Length();
                        if (length < 0.001f)
                        {
                            stack.Push(Value.ZEROVEC);
                            continue;
                        }
                        sum *= MathF.Pow(length, 1f / POWER - 1f); //если мы возводим ноль (или очень маленькое число) в отрицательную степень, то эта штука уходит на бесконечность
                        stack.Push(Value.VectorValue(sum));
                        break;
                    }
                case OP.MAX_TILE:
                    {
                        Vector2 pos = state.array[selfID].body.position.XY();
                        (int CX, int CY) = state.game.TileFromPoint(pos);

                        float max = -1e+6f;
                        int argX = -1, argY = -1;

                        //const int N = 2;
                        //for (int DY = -N; DY <= N; DY++)
                        for (int Y = 0; Y < state.VMY; Y++)
                        {
                            //int Y = CY + DY;
                            //if (Y < 0 || Y >= state.VMY) continue;
                            //for (int DX = -N; DX <= N; DX++)
                            for (int X = 0; X < state.VMX; X++)
                            {
                                //int X = CX + DX;
                                //if (X < 0 || X >= state.VMX) continue;

                                Vector2 v = state.game.PointFromTile(X, Y) - pos;
                                float f = Execute(state, selfID, storage, [Value.VectorValue(v)], opvariant).v.X;
                                if (f > max)
                                {
                                    max = f;
                                    argX = X;
                                    argY = Y;
                                }
                            }
                        }
                        Vector2 result = state.game.PointFromTile(argX, argY) - pos;
                        stack.Push(Value.VectorValue(result));
                        break;
                    }
                case OP.FOREACH:
                    {
                        Template template = state.game.templates[stack.Pop().i];
                        for (int i = 0; i < state.maxObjectCount; i++)
                        {
                            Object o = state.array[i];
                            if (o != null && o.template == template) Execute(state, selfID, storage, [Value.ObjectValue(i)], opvariant);
                        }
                        break;
                    }
                case OP.APPLY:
                    {
                        string ostr = strings[stack.Pop().i];
                        string istr = strings[stack.Pop().i];

                        Rule rule = Rule.Load(istr, ostr);

                        var matches = MJ.Matches(rule, state.voxelData, state.VMX, state.VMY, state.VMZ);
                        if (matches.Length > 0)
                        {
                            (Rule r, int x, int y, int z) = matches[state.lcg.Next(matches.Length)];
                            MJ.Apply(r, x, y, z, state.voxelData, state.VMX, state.VMY);
                            stack.Push(Value.TRUE);
                        }
                        else stack.Push(Value.FALSE);
                        break;
                    }
                case OP.DAMAGE: ///(damage target 1 self)
                    {
                        int targetID = stack.data[stack.numItems - 3].i;
                        Object target = stack.data[stack.numItems - 3].GetObject(state);
                        float damage = stack.data[stack.numItems - 2].v.X;
                        Object source = stack.data[stack.numItems - 1].GetObject(state);
                        stack.numItems -= 3; ///мы не хотим узнавать это число из рекордов, потому что мы не хотим делать Find() в рантайме

                        target.hp -= damage;
                        if (target.hp <= 0) state.toDestroy.Add(targetID);
                        break;
                    }
                case OP.PUTON:
                    {
                        int item = stack.data[stack.numItems - 2].i;
                        int surface = stack.data[stack.numItems - 1].i;
                        stack.numItems -= 2;

                        state.PutOn(item, surface);
                        break;
                    }
                case OP.DESTROY:
                    {
                        int targetID = stack.Pop().i;
                        if (targetID >= 0) state.toDestroy.Add(targetID);
                        break;
                    }
                case OP.DROP:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        state.Drop(self);
                        break;
                    }
                case OP.THROW:
                    {
                        Vector2 v = stack.Pop().v;
                        Object thrower = stack.Pop().GetObject(state);
                        state.Throw(thrower, v);
                        break;
                    }
                case OP.CREATE:
                    {
                        int freeIndex = state.FreeIndex();
                        Template template = state.game.templates[stack.Pop().i];
                        Vector3 position = state.RandomPosition(template.size, state.lcg);

                        if (freeIndex < 0 || float.IsInfinity(position.X)) { stack.Push(Value.NONE); break; }

                        Object o = new(template, state, freeIndex, position, VH.Unit(state.lcg), $"{template.displayName}{state.Amount(template)}");
                        state.array[freeIndex] = o;
                        state.maxObjectCount++;
                        stack.Push(Value.ObjectValue(freeIndex));
                        break;
                    }
                case OP.POSITION_FF:
                    {
                        float y = stack.data[stack.numItems - 1].v.X;
                        float x = stack.data[stack.numItems - 2].v.X;
                        Object o = stack.data[stack.numItems - 3].GetObject(state);
                        stack.numItems -= 3;

                        o.body.position = new Vector3(x, y, 0.5f * o.body.size.Z);
                        o.velocity = Vector3.Zero;
                        break;
                    }
                case OP.POSITION_V:
                    {
                        Vector2 v = stack.Pop().v;
                        Object o = stack.Pop().GetObject(state);

                        o.body.position = new Vector3(v, 0.5f * o.body.size.Z);
                        o.velocity = Vector3.Zero;
                        break;
                    }
                case OP.REWARD:
                    {
                        state.score += stack.Pop().v.X;
                        break;
                    }
                case OP.VOX:
                    {
                        int x = (int)stack.data[stack.numItems - 1].v.X;
                        int y = (int)stack.data[stack.numItems - 2].v.X;
                        int z = (int)stack.data[stack.numItems - 3].v.X;
                        state.voxelData[x + y * state.VMX + z * state.VMX * state.VMY] = (byte)stack.data[stack.numItems - 4].v.X;
                        stack.numItems -= 4;

                        break;
                    }
                case OP.RECT:
                    {
                        int X = (int)stack.data[stack.numItems - 1].v.X;
                        int Y = (int)stack.data[stack.numItems - 2].v.X;
                        int Z = (int)stack.data[stack.numItems - 3].v.X;
                        int WIDTH = (int)stack.data[stack.numItems - 4].v.X;
                        int HEIGHT = (int)stack.data[stack.numItems - 5].v.X;
                        byte value = (byte)stack.data[stack.numItems - 6].v.X;
                        stack.numItems -= 6;

                        for (int dy = 0; dy < HEIGHT; dy++) for (int dx = 0; dx < WIDTH; dx++) state.voxelData[X + dx + (Y + dy) * state.VMX + Z * state.VMX * state.VMY] = value;
                        break;
                    }
                case OP.PAR:
                    {
                        byte iva = (byte)stack.data[stack.numItems - 2].i;
                        byte ova = (byte)stack.data[stack.numItems - 1].i;
                        stack.numItems -= 2;

                        for (int z = State.SURFACE_TERRAIN_LAYER; z < state.VMZ; z++) for (int xy = 0; xy < state.VMX * state.VMY; xy++)
                        {
                            int i = xy + z * state.VMX * state.VMY;
                            if (state.voxelData[i] == iva) state.voxelData[i] = ova;
                        }
                        break;
                    }
                case OP.GO:
                    {
                        Vector2 v = stack.Pop().v;
                        stack.Push(Value.TupleValue(v == Vector2.Zero ? 0 : 1, false, v));
                        break;
                    }
                case OP.GOTO:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        Object goal = stack.Pop().GetObject(state);
                        if (goal == null) { stack.Push(Value.PASS); break; }
                        else
                        {
                            Vector2 v = (goal.body.position - self.body.position).XY();
                            stack.Push(Value.TupleValue(v == Vector2.Zero || !self.CanReach(goal) ? 0 : 1, false, v));
                            break;
                        }
                        //else if (goalValue.type == TYPE.VECTOR)
                        //{
                        //    Vector2 toGoal = goalValue.GetVector();
                        //    Vector2 goal = self.position.XY() + toGoal;
                        //    return Value.TupleValue(Object.BodyDistance(self, goal) <= 0f || !self.CanReach(goal) ? ACTION.TURN : ACTION.GO, toGoal);
                        //}
                    }
                case OP.USE:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        stack.Push(Value.TupleValue(0, true, self.dir));
                        break;
                    }
                case OP.COIN: stack.Push(state.lcg.NextBool() ? Value.TRUE : Value.FALSE); break;
                case OP.NEAR:
                    {
                        Object o2 = stack.Pop().GetObject(state);
                        Object o1 = stack.Pop().GetObject(state);

                        if (o1 == null || o2 == null) { stack.Push(Value.FALSE); break; }
                        stack.Push(Body.Near(o1.body, o2.body) ? Value.TRUE : Value.FALSE);
                        break;
                    }
                case OP.AND:
                    {
                        Value arg1 = stack.Pop();
                        Value arg0 = stack.Pop();
                        Value value = arg0.i == 0 ? arg0 : arg1;
                        if (value.type != TYPE.VOID) stack.Push(value);
                        break;
                    }
                case OP.OR:
                    {
                        Value arg1 = stack.Pop();
                        Value arg0 = stack.Pop();
                        Value value = arg0.i > 0 ? arg0 : arg1;
                        if (value.type != TYPE.VOID) stack.Push(value);
                        break;
                    }
                case OP.SEES:
                    {
                        Object o1 = stack.Pop().GetObject(state);
                        Object o0 = stack.Pop().GetObject(state);
                        if (o0 == null || o1 == null) { stack.Push(Value.FALSE); break; }

                        Vector2 v = Vector2.Normalize((o1.body.position - o0.body.position).XY());
                        if (Vector2.Dot(o0.dir, v) < o0.template.visionCos) { stack.Push(Value.FALSE); break; }
                        Object hit = Graphics.BodyHit2D(o0, v, state, o1, null).o;
                        stack.Push(hit == o1 ? Value.TRUE : Value.FALSE);
                        break;
                    }
                case OP.CHANGE_TILE_TO:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        byte c = (byte)stack.Pop().i;
                        (int x, int y) = state.game.TileFromPoint(self.body.position);
                        int index = x + y * state.VMX + State.SURFACE_TERRAIN_LAYER * state.VMX * state.VMY;
                        byte oldVoxel = state.voxelData[index];
                        state.voxelData[index] = c;
                        stack.Push(c != oldVoxel ? Value.TRUE : Value.FALSE);
                        break;
                    }
                case OP.TILE:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        (int x, int y) = state.game.TileFromPoint(self.body.position.XY() + stack.Pop().v);
                        int index = x + y * state.VMX + State.SURFACE_TERRAIN_LAYER * state.VMX * state.VMY;
                        stack.Push(x >= 0 && x < state.VMX && y >= 0 && y < state.VMY && state.voxelData[index] == MJ.dict['E'] ? Value.TRUE : Value.FALSE);
                        break;
                    }
                case OP.AMOUNT:
                    {
                        Template template = state.game.templates[stack.Pop().i];
                        stack.Push(Value.FloatValue(state.Amount(template)));
                        break;
                    }
                case OP.ALIGNMENT_VV: stack.Push(Value.FloatValue(MathF.Pow(0.5f * (1f + Vector2.Dot(Vector2.Normalize(stack.Pop().v), Vector2.Normalize(stack.Pop().v))), Settings.ALIGNMENT_POWER))); break;
                /*case OP.ALIGNMENT_VT:
                    {
                        Template t = game.templates[stack.Pop().i];
                        Vector2 v = stack.Pop().v;
                        Body body = storage.self.body;

                        float product = 1f;
                        for (int i = 0; i < state.objects.Count; i++)
                        {
                            Object o = state.objects[i];
                            if (o.template == t)
                            {
                                float d = Body.BodyDistance2D(o.body, body);
                                float weight = 25f / (d * d * d * d); //если кошка близко, то вес получается огромным. А если вес огромный, то произведение домножается на ноль. Мы возводим в большую степень, а значит придаём большую значимость
                                Vector2 too = (o.body.position - body.position).XY();
                                float alignment = 0.5f * (1f - Vector2.Dot(Vector2.Normalize(v), Vector2.Normalize(too))); ///so it's alignment from
                                product *= MathF.Pow(alignment, weight);
                            }
                        }
                        return Value.FloatValue(product);
                    }*/
                case OP.DIV_FF:
                    {
                        float f1 = stack.Pop().v.X;
                        float f0 = stack.Pop().v.X;
                        stack.Push(Value.FloatValue(f0 / f1));
                        break;
                    }
                case OP.POWER:
                    {
                        float f1 = stack.Pop().v.X;
                        float f0 = stack.Pop().v.X;
                        stack.Push(Value.FloatValue(MathF.Pow(f0, f1)));
                        break;
                    }
                case OP.SQUARE:
                    {
                        float x = stack.Pop().v.X;
                        stack.Push(Value.FloatValue(x * x));
                        break;
                    }
                case OP.UNIFORM:
                    {
                        float max = stack.Pop().v.X;
                        float min = stack.Pop().v.X;
                        float r = min + (max - min) * state.lcg.NextSingle();
                        stack.Push(Value.FloatValue(r));
                        break;
                    }
                case OP.DISTANCE_OO:
                    {
                        Object o1 = stack.Pop().GetObject(state);
                        Object o2 = stack.Pop().GetObject(state);
                        stack.Push(Value.FloatValue(o1 == null || o2 == null ? state.game.size.X + state.game.size.Y : (Body.BodyDistance2D(o1.body, o2.body) - Settings.NEAR_DISTANCE)));
                        break;
                    }
                case OP.RUNDIST:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        Template t = state.game.templates[stack.Pop().i];
                        Vector2 v = stack.Pop().v;

                        if (v.LengthSquared() < 0.000001f) { stack.Push(Value.FloatValue(-1f)); break; }
                        v = Vector2.Normalize(v);

                        Vector2 pmouse = self.body.position.XY();
                        float D = self.body.size.AverageRadius2D() + t.size.AverageRadius2D();
                        float k = t.speed / self.speed;
                        float omk2 = 1f - k * k;

                        float min = 1e+6f;
                        for (int i = 0; i < state.maxObjectCount; i++)
                        {
                            Object o = state.array[i];
                            if (o != null && o.template == t)
                            {
                                Vector2 DELTA = pmouse - o.body.position.XY();
                                float DELTA_SQ = DELTA.LengthSquared();
                                float DOT = Vector2.Dot(DELTA, v);

                                if (DELTA_SQ <= D * D) { stack.Push(Value.FloatValue(0f)); goto NextInstruction; }

                                float kDmDOT = k * D - DOT;
                                float DISCR = kDmDOT * kDmDOT - omk2 * (DELTA_SQ - D * D);
                                if (DOT < k * D && DISCR >= 0)
                                {
                                    float f = (kDmDOT - MathF.Sqrt(DISCR)) / omk2;
                                    if (f < min) min = f;
                                }
                            }
                        }

                        float raydist = Graphics.BodyHit2D(self, v, state, null, null).t;
                        float R = 0.25f * (state.game.size.X + state.game.size.Y), r = self.body.size.AverageRadius2D();
                        float dot = Vector2.Dot(v, pmouse);
                        float circdist = -dot + MathF.Sqrt(dot * dot + (R - r) * (R - r) - pmouse.LengthSquared());

                        stack.Push(Value.FloatValue(MathF.Min(circdist, MathF.Min(min, raydist))));
                        break;
                    }
                case OP.RAYDIST:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        Vector2 v = stack.Pop().v;
                        if (v.LengthSquared() < 0.000001f) { stack.Push(Value.FloatValue(-1f)); break; }

                        v = Vector2.Normalize(v);
                        float t = Graphics.BodyHit2D(self, v, state, null, null).t;
                        stack.Push(Value.FloatValue(t));
                        break;
                    }
                case OP.GAP:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        Vector2 v = stack.Pop().v;
                        if (v.LengthSquared() < 0.000001f) { stack.Push(Value.FloatValue(-1f)); break; }

                        v = Vector2.Normalize(v);
                        Body body = self.body;

                        const float DA = MathF.Tau / (4 * Settings.NUM_RAYS);
                        Vector2 l = v.Rotated(-DA);
                        Vector2 r = v.Rotated(DA);

                        float distv = Graphics.BodyHit2D(new Ray2(body.position.XY(), v), body.size.AverageRadius2D(), state, null, null).t;
                        float distl = Graphics.BodyHit2D(new Ray2(body.position.XY(), l), body.size.AverageRadius2D(), state, null, null).t;
                        float distr = Graphics.BodyHit2D(new Ray2(body.position.XY(), r), body.size.AverageRadius2D(), state, null, null).t;

                        float gap = MathF.Max(MathF.Abs(distv - distl), MathF.Abs(distv - distr));
                        stack.Push(Value.FloatValue(gap));
                        break;
                    }
                case OP.EQUIPPED: stack.Push(Value.ObjectValue(stack.Pop().GetObject(state).equippedUID)); break;
                case OP.HOLDER: stack.Push(Value.ObjectValue(stack.Pop().GetObject(state).holderUID)); break;
                case OP.CLOSEST_T:
                    {
                        bool all = stack.Pop().i == 0; ///GetList();
                        Template template = state.game.templates[stack.Pop().i];
                        Object origin = stack.Pop().GetObject(state);
                        if (origin == null) { stack.Push(Value.NONE); break; }

                        int argmin = -1;
                        float minDistance = 1000f;
                        for (int i = 0; i < state.maxObjectCount; i++)
                        {
                            Object o = state.array[i];
                            if (o != null && o.template == template && o != origin && o.holderUID < 0 && (all || Graphics.Sees(origin, o, state)))
                            {
                                float distance = Body.BodyDistance2D(origin.body, o.body);
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    argmin = i;
                                }
                            }
                        }
                        stack.Push(Value.ObjectValue(argmin));
                        break;
                    }
                case OP.BEAMCAST: //NEXT: видит сквозь воесели сейчас
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        Vector2 v = Vector2.Normalize(stack.Pop().v);
                        int result = Graphics.BeamObjectHit(self, v, state);
                        stack.Push(Value.ObjectValue(result));
                        break;
                    }
                case OP.DIR:
                    {
                        Object o = stack.Pop().GetObject(state);
                        stack.Push(o == null ? Value.ZEROVEC : Value.VectorValue(o.dir));
                        break;
                    }
                case OP.TO:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        int goalID = stack.Pop().i;
                        if (goalID < 0 || goalID == self.equippedUID) { stack.Push(Value.ZEROVEC); break; }

                        Vector2 v = (state.array[goalID].body.position - self.body.position).XY(); ///просто возвращаем разницу координат
                        float length = v.Length();
                        if (length < 0.01f) { stack.Push(Value.ZEROVEC); break; }
                        stack.Push(Value.VectorValue(v / length));
                        break;
                    }
                case OP.FROM:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        int goalID = stack.Pop().i;
                        if (goalID < 0 || goalID == self.equippedUID) { stack.Push(Value.ZEROVEC); break; }

                        Vector2 v = (state.array[goalID].body.position - self.body.position).XY();
                        float length = v.Length();
                        if (length < 0.01f) { stack.Push(Value.VectorValue(self.dir)); break; } ///важное отличие от TO: если мы уже стоим на месте цели, то нужно просто продолжать идти в текущем направлении
                        stack.Push(Value.VectorValue(-v / length)); ///а тут мы возвращаем не просто разницу, а норимированную разницу. И вроде как это сделано специально
                        break;
                    }
                case OP.RANDOM_DIRECTION:
                    {
                        float angle = 2f * MathF.PI * state.lcg.NextSingle();
                        Vector2 vec = new(MathF.Cos(angle), MathF.Sin(angle));
                        stack.Push(Value.VectorValue(vec));
                        break;
                    }
                case OP.UNIT:
                    {
                        float angle = 0.5f * MathF.PI * (1f - stack.Pop().v.X / 3f);
                        stack.Push(Value.VectorValue(VH.Unit(angle)));
                        break;
                    }
                case OP.PATH:
                    {
                        Object self = selfID >= 0 ? state.array[selfID] : null;
                        Object goal = stack.Pop().GetObject(state);

                        (int gx, int gy) = state.game.TileFromPoint(goal.body.position);

                        if (Graphics.Sees(self, goal, state))
                        {
                            Vector2 toGoal = Vector2.Normalize((goal.body.position - self.body.position).XY());
                            stack.Push(Value.VectorValue(toGoal));
                            break;
                        }

                        for (int y = 0; y < state.VMY; y++) for (int x = 0; x < state.VMX; x++)
                        {
                            int i = x + y * state.VMX;
                            state.field[i] = state.voxelData[i + State.SOLID_VOXEL_LAYER * state.VMX * state.VMY] > 0 ? -1 : -2;
                            state.directions[i] = 0xff;
                        }
                        state.field[gx + gy * state.VMX] = 0;

                        Queue<int> frontier = new();
                        frontier.Enqueue(gx + gy * state.VMX); ///will need to replace it with priority queue for A*
                        while (frontier.Count > 0)
                        {
                            int i = frontier.Dequeue();
                            int x = i % state.VMX, y = i / state.VMX;
                            int generation = state.field[i];
                            for (int d = 0; d < 4; d++)
                            {
                                int dx = Helper.DX[d];
                                int dy = Helper.DY[d];
                                int xn = x + dx;
                                int yn = y + dy;
                                int inew = xn + state.VMX * yn;
                                if (xn >= 0 && xn < state.VMX && yn >= 0 && yn < state.VMY && state.field[inew] == -2)
                                {
                                    state.field[inew] = generation + 1;
                                    state.directions[inew] = (byte)((d + 2) % 4);
                                    frontier.Enqueue(inew);

                                    Vector2 cellPosition = new Vector2(xn + 0.5f, yn + 0.5f) - 0.5f * state.game.size;
                                    if (Graphics.LOS(self, cellPosition, state)) { stack.Push(Value.VectorValue(Vector2.Normalize(cellPosition - self.body.position.XY()))); goto NextInstruction; }
                                }
                            }
                        }

                        (int sx, int sy) = state.game.TileFromPoint(self.body.position);
                        int dir = state.directions[sx + sy * state.VMX];
                        Vector2 v = VH.Unit(0.5f * dir * MathF.PI);
                        stack.Push(Value.VectorValue(v));
                        break;
                    }
                case OP.GET_TEMPLATE:
                    {
                        Object o = stack.Pop().GetObject(state);
                        if (o == null) { stack.Push(Value.NOTHING); break; }
                        int t = state.game.templates.IndexOf(o.template);
                        stack.Push(Value.TemplateValue(t));
                        break;
                    }
                case OP.SETSIZE:
                    {
                        state.CreateVoxels();
                        break;
                    }
                default:
                    throw new Exception($"unknown node {op}");
            }
        NextInstruction:
            continue;
        }

        Value returnValue = stack.numItems == stackStart ? Value.PASS : stack.Pop();
        stack.numItems = stackStart;
        if (state != null) state.inputs.numItems -= newInputs.Length;
        return returnValue;
    }
}
