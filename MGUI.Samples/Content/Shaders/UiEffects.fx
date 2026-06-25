float4x4 MatrixTransform;

float TimeSeconds;
float Opacity;
float2 ElementSize;
float2 ElementPosition;
float HoverAmount;
float PressAmount;
float SelectedAmount;
float DisabledAmount;
float ButtonRole;
float4 AccentColor;
float2 ElementTextureCoordinateScale;
float2 ElementTextureCoordinateOffset;
float2 TreatmentDirection;
float TreatmentStrength;

sampler2D SpriteTextureSampler : register(s0);

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct PixelShaderInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

PixelShaderInput SpriteVertexShader(VertexShaderInput input)
{
    PixelShaderInput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}

float EdgeDistance(float2 uv)
{
    float2 edge = min(uv, 1.0 - uv);
    return min(edge.x * ElementSize.x, edge.y * ElementSize.y);
}

float4 UiPixelShader(PixelShaderInput input) : COLOR0
{
    float2 textureCoordinate = input.TextureCoordinate;
    float2 uv = saturate(textureCoordinate);
    float edgeDistance = EdgeDistance(uv);
    float rim = 1.0 - smoothstep(0.5, 3.0, edgeDistance);
    float3 baseColor = AccentColor.rgb * lerp(0.34, 0.48, saturate(ButtonRole));
    float4 result = float4(baseColor, AccentColor.a);
    float4 sampledColor = tex2D(SpriteTextureSampler, textureCoordinate);

    float2 wholeElementCoordinate =
        textureCoordinate * ElementTextureCoordinateScale + ElementTextureCoordinateOffset;
    float directionLength = max(length(TreatmentDirection), 0.0001);
    float directionalAmount = dot(
        wholeElementCoordinate - float2(0.5, 0.5),
        TreatmentDirection / directionLength);
    float directionalTreatment = 1.0 + directionalAmount * TreatmentStrength;
    sampledColor.rgb *= directionalTreatment;

    if (ButtonRole > 1.5)
    {
        float pulse = 0.5 + 0.5 * sin(TimeSeconds * 4.5);
        result.rgb *= 0.72 + pulse * 0.38;
    }

    result.rgb = lerp(result.rgb, AccentColor.rgb * 0.78, SelectedAmount);
    result.rgb += AccentColor.rgb * rim * HoverAmount * 0.38;
    result.rgb *= 1.0 - PressAmount * 0.32;
    float gray = dot(result.rgb, float3(0.299, 0.587, 0.114));
    result.rgb = lerp(result.rgb, float3(gray, gray, gray) * 0.62, DisabledAmount);

    // Opacity is contextual only: input.Color already carries draw opacity, so alpha is not multiplied twice.
    result.rgb = lerp(result.rgb * 0.92, result.rgb, saturate(Opacity));
    return input.Color * result * sampledColor;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 UiPixelShader();
    }
}
