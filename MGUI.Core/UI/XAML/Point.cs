using Microsoft.Xna.Framework;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace MGUI.Core.UI.XAML
{
    [TypeConverter(typeof(PointStringConverter))]
    public readonly record struct XAMLPoint(int X, int Y)
    {
        public Point ToPoint() => new(X, Y);
    }

    public class PointStringConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type SourceType)
            => SourceType == typeof(string) || base.CanConvertFrom(context, SourceType);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo Culture, object Value)
        {
            if (Value is string StringValue)
            {
                int[] Components = StringValue.Split(',')
                    .Select(x => int.Parse(x.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture))
                    .ToArray();
                if (Components.Length == 2)
                {
                    return new XAMLPoint(Components[0], Components[1]);
                }

                throw new FormatException($"'{StringValue}' is not a valid point. Expected two comma-separated integers, such as '1,2'.");
            }

            return base.ConvertFrom(context, Culture, Value);
        }
    }
}
