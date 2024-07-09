Shader "Unlit/ZPlayerScreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        //_MetaPassEmissiveBoost("Meta Pass Emissive Boost", Float) = 1.0

        [ToggleUI]_Transparent("Transparent Border", Int) = 1
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
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;

            float4 _MainTex_TexelSize;
            float _IsAVProInput;
            float _TargetAspectRatio;
            half _Transparent;
            #define _EmissionColor float3(1, 1, 1)

            half4 VideoEmission(float2 uv)
            {
                //https://bgolus.medium.com/sharper-mipmapping-using-shader-based-supersampling-ed7aadb47bec
                // per pixel partial derivatives
                float2 dx = ddx(uv.xy);
                float2 dy = ddy(uv.xy);
                // rotated grid uv offsets
                float2 uvOffsets = float2(0.125, 0.375);
                float bias = -1.0;
                float4 offsetUV = float4(0.0, 0.0, 0.0, bias);
                // supersampled using 2x2 rotated grid
                half4 col = 0;
                offsetUV.xy = uv.xy + uvOffsets.x * dx + uvOffsets.y * dy;
                col += tex2Dbias(_MainTex, offsetUV);
                offsetUV.xy = uv.xy - uvOffsets.x * dx - uvOffsets.y * dy;
                col += tex2Dbias(_MainTex, offsetUV);
                offsetUV.xy = uv.xy + uvOffsets.y * dx - uvOffsets.x * dy;
                col += tex2Dbias(_MainTex, offsetUV);
                offsetUV.xy = uv.xy - uvOffsets.y * dx + uvOffsets.x * dy;
                col += tex2Dbias(_MainTex, offsetUV);
                col *= 0.25;

                col.rgb *= _EmissionColor.rgb;
                return col;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); //Insert
                UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = VideoEmission(i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);
                half alpha = _Transparent ? col.a : 1;
                return half4(col.rgb, alpha);
            }
            ENDCG
        }
    }
}

// 
// MIT License

// Copyright (c) 2020 Merlin

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.