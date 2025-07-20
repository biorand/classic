using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand
{
    public sealed class ScdCondition
    {
        public IExpression Expression { get; }

        public ScdCondition(IExpression expression)
        {
            Expression = expression;
        }

        public OpcodeBase[] Generate(BioVersion version, IEnumerable<OpcodeBase> children)
        {
            return version switch
            {
                BioVersion.Biohazard1 => Generate1(children),
                BioVersion.Biohazard2 => Generate2(children),
                BioVersion.Biohazard3 => Generate3(children),
                BioVersion.BiohazardCv => GenerateCv(children),
                _ => throw new NotImplementedException(),
            };
        }

        public OpcodeBase[] Generate1(IEnumerable<OpcodeBase> children)
        {
            // We might need to split the children into multiple blocks if they are too large
            // IF / ELSE jumps only stored as a byte
            const int maxBlockSize = 256 - 64; // 64 bytes reserved for the IF AND CK opcodes

            var total = new List<OpcodeBase>();
            var childrenArray = children.ToArray();
            var blockSize = 0;
            var block = new List<OpcodeBase>();
            for (var i = 0; i < childrenArray.Length; i++)
            {
                var opcode = childrenArray[i];
                if (blockSize + opcode.Length > maxBlockSize)
                {
                    total.AddRange(Generate1Split(block));
                    block.Clear();
                    blockSize = 0;
                }
                block.Add(opcode);
                blockSize += opcode.Length;
            }
            if (block.Count != 0)
            {
                total.AddRange(Generate1Split(block));
            }
            return total.ToArray();
        }

        private OpcodeBase[] Generate1Split(IEnumerable<OpcodeBase> children)
        {
            var result = new List<OpcodeBase>();

            var ifOpcode = new UnknownOpcode(0, (byte)OpcodeV1.IfelCk, new byte[] { 0x00 });
            result.Add(ifOpcode);

            var negated = false;
            var expr = Expression;
            if (expr is Negated negatedNandExpression)
            {
                expr = negatedNandExpression.Child;
                negated = true;
            }

            var expressions = new Stack<IExpression>();
            while (expr is And and)
            {
                expressions.Push(and.Right);
                expr = and.Left;
            }
            expressions.Push(expr);

            while (expressions.Count != 0)
            {
                expr = expressions.Pop();
                if (expr is Flag flagExpr)
                {
                    result.Add(new UnknownOpcode(0, (byte)OpcodeV1.Ck, new byte[] { flagExpr.Group, (byte)flagExpr.Index, (byte)(flagExpr.Value ? 1 : 0) }));
                }
                else if (expr is Variable varExpr)
                {
                    throw new NotImplementedException();
                }
            }

            UnknownOpcode? elseOpcode = null;
            if (negated)
            {
                // Insert else opcodes
                elseOpcode = new UnknownOpcode(0, (byte)OpcodeV1.ElseCk, new byte[] { 0 });
                result.Add(elseOpcode);
                result.AddRange(children);
            }
            else
            {
                result.AddRange(children);
                result.Add(new UnknownOpcode(0, (byte)OpcodeV1.EndIf, new byte[] { 0 }));
            }

            var offset = 0;
            foreach (var opcode in result)
            {
                opcode.Offset = offset;
                offset += opcode.Length;
            }

            var ifAddress = ifOpcode.Offset;
            if (negated)
            {
                ifAddress = (elseOpcode!.Offset + elseOpcode.Length) - ifAddress - 2;
                var elseAddress = offset - elseOpcode.Offset;
                elseOpcode.Data[0] = (byte)elseAddress;
            }
            else
            {
                ifAddress = offset - ifAddress - 2;
            }
            ifOpcode.Data[0] = (byte)ifAddress;

            return result.ToArray();
        }

        public OpcodeBase[] Generate2(IEnumerable<OpcodeBase> children)
        {
            var result = new List<OpcodeBase>();

            var ifOpcode = new UnknownOpcode(0, (byte)OpcodeV2.IfelCk, new byte[] { 0x00, 0x00, 0x00 });
            result.Add(ifOpcode);

            var negated = false;
            var expr = Expression;
            if (expr is Negated negatedNandExpression)
            {
                expr = negatedNandExpression.Child;
                negated = true;
            }

            var expressions = new Stack<IExpression>();
            while (expr is And and)
            {
                expressions.Push(and.Right);
                expr = and.Left;
            }
            expressions.Push(expr);

            while (expressions.Count != 0)
            {
                expr = expressions.Pop();
                if (expr is Flag flagExpr)
                {
                    result.Add(new UnknownOpcode(0, (byte)OpcodeV2.Ck, new byte[] { flagExpr.Group, (byte)flagExpr.Index, (byte)(flagExpr.Value ? 1 : 0) }));
                }
                else if (expr is Variable varExpr)
                {
                    result.Add(new UnknownOpcode(0, (byte)OpcodeV2.Cmp, new byte[] { 0x00, varExpr.Index, (byte)(varExpr.Negated ? 5 : 0), (byte)(varExpr.Value & 0xFF), (byte)((varExpr.Value >> 8) & 0xFF) }));
                }
            }

            UnknownOpcode? elseOpcode = null;
            if (negated)
            {
                // Insert else opcodes
                elseOpcode = new UnknownOpcode(0, (byte)OpcodeV2.ElseCk, new byte[] { 0, 0, 0 });
                result.Add(elseOpcode);
                result.AddRange(children);
            }
            else
            {
                result.AddRange(children);
                result.Add(new UnknownOpcode(0, (byte)OpcodeV2.EndIf, new byte[] { 0 }));
            }

            var offset = 0;
            foreach (var opcode in result)
            {
                opcode.Offset = offset;
                offset += opcode.Length;
            }

            var ifAddress = ifOpcode.Offset;
            if (negated)
            {
                ifAddress = (elseOpcode!.Offset + elseOpcode.Length) - (ifAddress + 4);
                var elseAddress = offset - elseOpcode.Offset;
                elseOpcode.Data[1] = (byte)(elseAddress & 0xFF);
                elseOpcode.Data[2] = (byte)((elseAddress >> 8) & 0xFF);
            }
            else
            {
                ifAddress = offset - (ifAddress + 4);
            }
            ifOpcode.Data[1] = (byte)(ifAddress & 0xFF);
            ifOpcode.Data[2] = (byte)((ifAddress >> 8) & 0xFF);

            return result.ToArray();
        }

        public OpcodeBase[] Generate3(IEnumerable<OpcodeBase> children)
        {
            var result = new List<OpcodeBase>();

            var ifOpcode = new UnknownOpcode(0, (byte)OpcodeV3.IfelCk, new byte[] { 0x00, 0x00, 0x00 });
            result.Add(ifOpcode);

            var negated = false;
            var expr = Expression;
            if (expr is Negated negatedNandExpression)
            {
                expr = negatedNandExpression.Child;
                negated = true;
            }

            var expressions = new Stack<IExpression>();
            while (expr is And and)
            {
                expressions.Push(and.Right);
                expr = and.Left;
            }
            expressions.Push(expr);

            while (expressions.Count != 0)
            {
                expr = expressions.Pop();
                if (expr is Flag flagExpr)
                {
                    result.Add(new UnknownOpcode(0, (byte)OpcodeV3.Ck, new byte[] { flagExpr.Group, (byte)flagExpr.Index, (byte)(flagExpr.Value ? 1 : 0) }));
                }
                else if (expr is Variable varExpr)
                {
                    result.Add(new UnknownOpcode(0, (byte)OpcodeV3.Cmp, new byte[] { 0x00, varExpr.Index, (byte)(varExpr.Negated ? 5 : 0), (byte)(varExpr.Value & 0xFF), (byte)((varExpr.Value >> 8) & 0xFF) }));
                }
            }

            UnknownOpcode? elseOpcode = null;
            if (negated)
            {
                // Insert else opcodes
                elseOpcode = new UnknownOpcode(0, (byte)OpcodeV3.ElseCk, new byte[] { 0, 0, 0 });
                result.Add(elseOpcode);
                result.AddRange(children);
            }
            else
            {
                result.AddRange(children);
                result.Add(new UnknownOpcode(0, (byte)OpcodeV3.EndIf, new byte[] { 0 }));
            }

            var offset = 0;
            foreach (var opcode in result)
            {
                opcode.Offset = offset;
                offset += opcode.Length;
            }

            var ifAddress = ifOpcode.Offset;
            if (negated)
            {
                ifAddress = (elseOpcode!.Offset + elseOpcode.Length) - (ifAddress + 4);
                var elseAddress = offset - elseOpcode.Offset;
                elseOpcode.Data[1] = (byte)(elseAddress & 0xFF);
                elseOpcode.Data[2] = (byte)((elseAddress >> 8) & 0xFF);
            }
            else
            {
                ifAddress = offset - (ifAddress + 4);
            }
            ifOpcode.Data[1] = (byte)(ifAddress & 0xFF);
            ifOpcode.Data[2] = (byte)((ifAddress >> 8) & 0xFF);

            return result.ToArray();
        }

        public OpcodeBase[] GenerateCv(IEnumerable<OpcodeBase> children)
        {
            var result = new List<OpcodeBase>();

            var ifOpcode = new UnknownOpcode(0, 0x01, new byte[] { 0x00 });
            result.Add(ifOpcode);

            var negated = false;
            var expr = Expression;
            if (expr is Negated negatedNandExpression)
            {
                expr = negatedNandExpression.Child;
                negated = true;
            }

            var expressions = new Stack<IExpression>();
            while (expr is And and)
            {
                expressions.Push(and.Right);
                expr = and.Left;
            }
            expressions.Push(expr);

            while (expressions.Count != 0)
            {
                expr = expressions.Pop();
                if (expr is Flag flagExpr)
                {
                    result.Add(new UnknownOpcode(0, 0x04, new byte[] { flagExpr.Group, (byte)(flagExpr.Index & 0xFF), (byte)((flagExpr.Index >> 8) & 0xFF), 0, (byte)(flagExpr.Value ? 1 : 0) }));
                }
                else if (expr is Variable varExpr)
                {
                    throw new NotImplementedException();
                }
            }

            UnknownOpcode? elseOpcode = null;
            if (negated)
            {
                // Insert else opcodes
                elseOpcode = new UnknownOpcode(0, 0x02, new byte[] { 0x00 });
                result.Add(elseOpcode);
                result.AddRange(children);
            }
            else
            {
                result.AddRange(children);
                result.Add(new UnknownOpcode(0, 0x03, new byte[] { 0 }));
            }

            var offset = 0;
            foreach (var opcode in result)
            {
                opcode.Offset = offset;
                offset += opcode.Length;
            }

            var ifAddress = ifOpcode.Offset;
            if (negated)
            {
                ifAddress = (elseOpcode!.Offset + elseOpcode.Length) - (ifAddress + 2);
                var elseAddress = offset - elseOpcode.Offset;
                elseOpcode.Data[0] = (byte)elseAddress;
            }
            else
            {
                ifAddress = offset - (ifAddress + 2);
            }
            ifOpcode.Data[0] = (byte)ifAddress;

            return result.ToArray();
        }

        public override string ToString() => Expression.ToString();

        public static ScdCondition Parse(string condition)
        {
            var negated = false;
            var negatedMatch = Regex.Match(condition, "!\\((.*)\\)");
            if (negatedMatch.Success)
            {
                negated = true;
                condition = negatedMatch.Groups[1].Value;
            }

            var conditions = condition
                .Replace("&&", "&")
                .Split('&')
                .Select(x => x.Trim())
                .ToArray();

            IExpression? leftExpr = null;
            var ckOpcodes = new List<OpcodeBase>();
            foreach (var c in conditions)
            {
                var m = Regex.Match(c, "(!?)(\\d+):(\\d+)");
                if (m.Success)
                {
                    var value = m.Groups[1].Value == "!" ? (byte)0 : (byte)1;
                    var left = byte.Parse(m.Groups[2].Value);
                    var right = ushort.Parse(m.Groups[3].Value);
                    IExpression expr = new Flag(left, right, value == 1);
                    leftExpr = leftExpr == null ? expr : new And(leftExpr, expr);
                }
                else
                {
                    m = Regex.Match(c, "\\$(\\d+)\\s*(!=|==)\\s*(\\d+)");
                    if (m.Success)
                    {
                        var var = byte.Parse(m.Groups[1].Value);
                        var op = m.Groups[2].Value == "==" ? (byte)0 : (byte)5;
                        var value = short.Parse(m.Groups[3].Value);
                        IExpression expr = new Variable(var, value, op != 0);
                        leftExpr = leftExpr == null ? expr : new And(leftExpr, expr);
                    }
                }
            }

            if (leftExpr == null)
                throw new ArgumentException();

            if (negated)
                leftExpr = new Negated(leftExpr);

            return new ScdCondition(leftExpr);
        }

        public interface IExpression
        {
        }

        public class Flag : IExpression
        {
            public byte Group { get; set; }
            public ushort Index { get; set; }
            public bool Value { get; set; }

            public Flag(byte group, ushort index, bool value)
            {
                Group = group;
                Index = index;
                Value = value;
            }

            public override string ToString() => Value ? $"{Group}:{Index}" : $"!{Group}:{Index}";
        }

        public class Variable : IExpression
        {
            public byte Index { get; set; }
            public short Value { get; set; }
            public bool Negated { get; set; }

            public Variable(byte index, short value, bool negated)
            {
                Index = index;
                Value = value;
                Negated = negated;
            }

            public override string ToString() => Negated ? $"${Index} != {Value}" : $"${Index} == {Value}";
        }

        public class Negated : IExpression
        {
            public IExpression Child { get; }

            public Negated(IExpression left)
            {
                Child = left;
            }

            public override string ToString() => $"!({Child})";
        }

        public class And : IExpression
        {
            public IExpression Left { get; }
            public IExpression Right { get; }

            public And(IExpression left, IExpression right)
            {
                Left = left;
                Right = right;
            }

            public override string ToString() => $"{Left} && {Right}";
        }
    }
}
