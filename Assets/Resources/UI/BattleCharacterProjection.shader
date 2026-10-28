Shader "MageKnight/UI/BattleCharacterProjection"
{
    Properties
    {
        [PerRendererData] _MainTex("Character alpha",2D)="white"{}
        _Opacity("Ground opacity",Range(0,1))=.28
        _BodyUV("Visible vertical UV",Vector)=(0,1,0,0)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags{"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        Stencil{Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
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
            struct appdata{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f{float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 position:TEXCOORD1;};
            sampler2D _MainTex;float _Opacity;float4 _BodyUV,_ClipRect;
            v2f vert(appdata v){v2f o;o.position=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            float4 frag(v2f i):SV_Target
            {
                float2 d=float2(.008,.04);
                float a=tex2D(_MainTex,i.uv).a*.5;
                a+=(tex2D(_MainTex,i.uv+float2(d.x,0)).a+tex2D(_MainTex,i.uv-float2(d.x,0)).a
                    +tex2D(_MainTex,i.uv+float2(0,d.y)).a+tex2D(_MainTex,i.uv-float2(0,d.y)).a)*.125;
                float y=saturate((i.uv.y-_BodyUV.x)/max(_BodyUV.y-_BodyUV.x,.001));
                float4 result=float4(.018,.025,.022,a*_Opacity*(1-y*.8)*i.color.a);
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
