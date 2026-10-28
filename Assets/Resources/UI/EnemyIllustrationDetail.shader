Shader "MageKnight/UI/EnemyIllustrationDetail"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original enemy scan", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 position : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Color, _ClipRect, _TextureSampleAdd;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            // Catmull-Rom reconstruction samples exact texel centres. Bilinear
            // sampling followed by fine sharpening reinforces the scan's square
            // texels/print dots; reconstruct a continuous edge before enhancing it.
            float4 CubicWeights(float t)
            {
                float t2 = t * t, t3 = t2 * t;
                return float4(-.5*t + t2 - .5*t3,
                    1 - 2.5*t2 + 1.5*t3,
                    .5*t + 2*t2 - 1.5*t3, -.5*t2 + .5*t3);
            }
            float4 Reconstruct(float2 uv)
            {
                float2 size = abs(_MainTex_TexelSize.zw);
                float2 p = uv * size - .5;
                float2 origin = floor(p);
                float4 wx = CubicWeights(frac(p.x));
                float4 wy = CubicWeights(frac(p.y));
                float4 sum = 0;
                float3 low = 1, high = 0;
                [unroll] for (int y = 0; y < 4; y++)
                {
                    [unroll] for (int x = 0; x < 4; x++)
                    {
                        float2 sampleUv = (origin + float2(x - 1, y - 1) + .5) / size;
                        float4 sampleColor = tex2D(_MainTex, sampleUv);
                        sum += sampleColor * wx[x] * wy[y];
                        if (x >= 1 && x <= 2 && y >= 1 && y <= 2)
                        {
                            low = min(low, sampleColor.rgb);
                            high = max(high, sampleColor.rgb);
                        }
                    }
                }
                // Negative cubic lobes must not create bright/dark ringing.
                return float4(clamp(sum.rgb, low, high), saturate(sum.a));
            }
            float4 frag(v2f i) : SV_Target
            {
                float4 original = Reconstruct(i.uv) + _TextureSampleAdd;
                // Work on equipment/face contours, not the 1-2 texel print dots.
                // Native scan radius remains stable across all enemy card widths.
                float2 d = abs(_MainTex_TexelSize.xy) * 4.0;
                float3 a = tex2D(_MainTex, i.uv + float2(d.x, 0)).rgb;
                float3 b = tex2D(_MainTex, i.uv - float2(d.x, 0)).rgb;
                float3 c = tex2D(_MainTex, i.uv + float2(0, d.y)).rgb;
                float3 e = tex2D(_MainTex, i.uv - float2(0, d.y)).rgb;
                float3 blur = (a + b + c + e) * .25;
                float3 detail = clamp((original.rgb - blur) * .65, -.075, .075);
                float3 low = min(original.rgb, min(min(a, b), min(c, e)));
                float3 high = max(original.rgb, max(max(a, b), max(c, e)));
                float4 result = float4(clamp(original.rgb + detail, low, high), original.a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.position.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - .001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
