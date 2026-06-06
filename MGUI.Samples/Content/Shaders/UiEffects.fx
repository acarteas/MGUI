float4x4 MatrixTransform;

float TimeSeconds;
float Opacity;
float2 ElementSize;
float2 ElementPosition;
float HoverAmount;
float PressAmount;
float FocusAmount;
float Mode;
float4 ColorA;
float4 ColorB;

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
    float2 uv = saturate((input.Position.xy - ElementPosition) / max(ElementSize, float2(1.0, 1.0)));
    float4 result = lerp(ColorA, ColorB, uv.x);

    if (Mode < 0.5)
    {
        float pulse = 0.5 + 0.5 * sin(TimeSeconds * 4.5);
        result.rgb *= 0.45 + pulse * 0.95;
    }
    else if (Mode < 1.5)
    {
        float sweep = smoothstep(0.0, 1.0, 1.0 - abs((uv.x - 0.5) * 2.0));
        result.rgb = lerp(ColorA.rgb, saturate(ColorB.rgb + sweep * 0.35), HoverAmount);
    }
    else if (Mode < 2.5)
    {
        float active = max(FocusAmount, PressAmount);
        result.rgb = lerp(ColorA.rgb * 0.55, ColorB.rgb, active);
        result.rgb *= 1.0 - PressAmount * 0.35;
    }
    else if (Mode < 3.5)
    {
        float edgeDistance = EdgeDistance(uv);
        float rim = 1.0 - smoothstep(0.5, 2.5, edgeDistance);
        float halo = 1.0 - smoothstep(1.5, 12.0, edgeDistance);
        float pulse = 0.65 + 0.35 * sin(TimeSeconds * 3.2);
        float glow = saturate(rim + halo * pulse * 0.75) * FocusAmount;
        float3 glowColor = saturate(ColorB.rgb * (1.15 + rim * 0.45));
        result.rgb = lerp(ColorA.rgb * 0.42, glowColor, glow);
    }
    else
    {
        float gray = dot(result.rgb, float3(0.299, 0.587, 0.114));
        result.rgb = lerp(float3(gray, gray, gray), result.rgb, 0.22);
        result.rgb *= 0.62;
    }

    result.a *= Opacity;
    return input.Color * result;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 UiPixelShader();
    }
}
