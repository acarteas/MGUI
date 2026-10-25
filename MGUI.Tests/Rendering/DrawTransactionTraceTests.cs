using System.Reflection;
using System.Reflection.Emit;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Rendering;

public sealed class DrawTransactionTraceTests
{
    [Fact]
    public void DefaultBuildContainsNoTraceStorageOrDrawTransactionCalls()
    {
        Assert.Empty(typeof(DrawTransactionTrace).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
            .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => unchecked((ushort)code.Value));
        foreach (MethodBase method in new[] { typeof(DrawTransaction), typeof(MGUI.Core.UI.MGDesktop).Assembly.GetType("MGUI.Core.UI.Brushes.Fill_Brushes.MGEffectBinding")! }
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Cast<MethodBase>().Concat(type.GetConstructors())))
        {
            byte[]? body = method.GetMethodBody()?.GetILAsByteArray();
            if (body == null)
            {
                continue;
            }
            for (int index = 0; index < body.Length;)
            {
                ushort value = body[index++];
                if (value == 0xfe)
                {
                    value = (ushort)(0xfe00 | body[index++]);
                }
                OpCode code = opcodes[value];
                if (code.OperandType == OperandType.InlineMethod)
                {
                    MethodBase? target = method.Module.ResolveMethod(BitConverter.ToInt32(body, index));
                    Assert.NotEqual(typeof(DrawTransactionTrace), target?.DeclaringType);
                }
                index += code.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + BitConverter.ToInt32(body, index) * 4,
                    _ => 4
                };
            }
        }
    }
}
