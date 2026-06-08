float4x4 MatrixTransform;
float2 ElementPosition;
float2 ElementSize;
float Opacity;
float TimeSeconds;
float HoverAmount;
float PressAmount;
float SelectedAmount;
float DisabledAmount;
float CustomFloat;
int CustomInt;
bool CustomBool;
float2 CustomVector2;
float3 CustomVector3;
float4 CustomVector4;
float4 CustomColor;
float IncompatibleScalar;

struct VertexShaderInput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TextureCoordinate : TEXCOORD0; };
struct PixelShaderInput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TextureCoordinate : TEXCOORD0; };

PixelShaderInput SpriteVertexShader(VertexShaderInput input)
{
    PixelShaderInput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}

float4 SpritePixelShader(PixelShaderInput input) : COLOR0
{
    float keep = CustomFloat + CustomInt + (CustomBool ? 1.0 : 0.0) + CustomVector2.x + CustomVector3.x +
        CustomVector4.x + CustomColor.x + IncompatibleScalar + ElementPosition.x + ElementSize.x + Opacity +
        TimeSeconds + HoverAmount + PressAmount + SelectedAmount + DisabledAmount;
    return input.Color + keep * 0.0000001;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
