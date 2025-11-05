// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "Unlit/ZPlayer/UI Screen"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // UIPlus        
        [Header(Sampling)]
        [ToggleOff(_SUPERSAMPLE_OFF)] _SuperSampling ("Super Sampling", Integer) = 1
        [Toggle(_MSDF)] _MSDF ("MSDF", Integer) = 0

        [Header(Border)]
        [ToggleUI] _BorderEnabled ("Enabled", Integer) = 0
        _Border ("Border Width", Float) = 0
        _OutlineColor ("Border Color", Color) = (1,1,1,1)
        _Radius ("Radius", Vector) = (0,0,0,0)

        [Header(Glass)]
        [Toggle(_GLASS)] _Glass ("Glass", Integer) = 0
        _GlassBlend ("Blend", Range(0, 1)) = 0
        [Toggle(_BLUR)] _GlassBlur ("Blur", Integer) = 0
        _BlurMip ("Blur Mip", Integer) = 0
        _IOR ("IOR", Range(1.0, 1.5)) = 1.1
        _GlassThickness ("Thickness", Float) = 20

        [NoScaleOffset] _Background ("Background", 2D) = "white" {}
        _BackgroundColor ("Background Tint", Color) = (1,1,1,1)
        _CanvasSize ("Canvas Size", Vector) = (1000, 1000, 0, 0)

        _TargetAspectRatio ("Target Aspect Ratio", Float) = 1.7777777
        [NoScaleOffset] _VideoTex ("Video Tex", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #pragma shader_feature_local_fragment _GLASS
            #pragma shader_feature_local_fragment _BLUR
            #pragma shader_feature_local_fragment _SUPERSAMPLE_OFF
            #pragma shader_feature_local_fragment _MSDF

            #include "UnityCG.cginc"
            #define _MainTex _VideoTex
            #define _MainTex_ST _VideoTex_ST
            #define _MainTex_TexelSize _VideoTex_TexelSize
            #include "Packages/com.z3y.uiplus/Runtime/Shader/UI.hlsl"

            half _TargetAspectRatio;

            void ApplyVideoTexture(inout float2 uv, out float2 canvasSize)
            {
                float4 texelSize = _MainTex_TexelSize;
                
                float2 normalizedVideoRes = float2(texelSize.y / _TargetAspectRatio, texelSize.x);
                float2 correctiveScale;
                    
                // Find which axis is greater, we will clamp to that
                if (normalizedVideoRes.x > normalizedVideoRes.y)
                    correctiveScale = float2(1, normalizedVideoRes.y / normalizedVideoRes.x);
                else
                    correctiveScale = float2(normalizedVideoRes.x / normalizedVideoRes.y, 1);

                canvasSize = _CanvasSize.xy * correctiveScale * float2(_TargetAspectRatio, 1);
                // uv -= 0.5;
                // uv.x *= aspect;
                // uv.x *= _TargetAspectRatio * ;
                // canvasSize.x *= _TargetAspectRatio;
                // uv += 0.5;

                uv = ((uv - 0.5) / correctiveScale) + 0.5;
            }

            half4 frag(v2f IN) : SV_Target
            {
                // float2 uv = IN.texcoord.xy;
                // float aspect = texelSize.x / texelSize.y;
                // float2 normalizedVideoRes = float2(texelSize.y / _TargetAspectRatio, texelSize.x);
                // float2 correctiveScale;
                    
                    // // Find which axis is greater, we will clamp to that
                    // if (normalizedVideoRes.x > normalizedVideoRes.y)
                    //     correctiveScale = float2(1, normalizedVideoRes.y / normalizedVideoRes.x);
                    // else
                    //     correctiveScale = float2(normalizedVideoRes.x / normalizedVideoRes.y, 1);

                    // // uv = ((uv - 0.5) / correctiveScale) + 0.5;


                float2 rectSize;

                // uv -= 0.5;
                // uv.x *= aspect;
                // uv.x *= _TargetAspectRatio;
                // rectSize.x *= _TargetAspectRatio;
                // uv += 0.5;

                ApplyVideoTexture(IN.texcoord.xy, rectSize);
                ApplyVideoTexture(IN.texcoordCentroid.xy, rectSize);

                IN.rectSize.xy = rectSize;
                IN.texcoord.zw = (IN.texcoord.xy - 0.5) * rectSize;
half4 mainTexture = tex2D(_MainTex, IN.texcoord.xy);
// return mainTexture;

                half4 color = ApplyUI(IN);

                color.r = GammaToLinearSpaceExact(color.r);
                color.g = GammaToLinearSpaceExact(color.g);
                color.b = GammaToLinearSpaceExact(color.b);

                return color;
            }
        ENDCG
        }
    }
}