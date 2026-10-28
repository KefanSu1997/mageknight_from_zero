Shader "MageKnight/UI/BattleCharacterAtmosphere"
{
    Properties
    {
        [PerRendererData] _MainTex ("Character", 2D) = "white" {}
        _Color ("State tint", Color) = (1,1,1,1)
        _TopLight ("Forest upper light", Color) = (1,.98,.94,1)
        _GroundLight ("Ground ambient", Color) = (.88,.92,.96,1)
        _Saturation ("Pigment saturation", Range(0,1)) = .9
        _BodyUV ("Visible vertical UV range", Vector) = (0,1,0,0)
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
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 position:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _Color, _TopLight, _GroundLight, _BodyUV, _ClipRect, _TextureSampleAdd;
            float _Saturation;
            v2f vert(appdata v)
            {
                v2f o; o.position=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 source=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                float luminance=dot(source.rgb,float3(.2126,.7152,.0722));
                float bodyY=saturate((i.uv.y-_BodyUV.x)/max(_BodyUV.y-_BodyUV.x,.001));
                float3 light=lerp(_GroundLight.rgb,_TopLight.rgb,smoothstep(0,1,bodyY));
                // Runtime environmental grading only: original image, UV, shape,
                // alpha and readiness/death state tint remain intact.
                float4 result=float4(lerp(luminance.xxx,source.rgb,_Saturation)*light,source.a)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.position.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
