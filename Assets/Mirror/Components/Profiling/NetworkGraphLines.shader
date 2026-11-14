Shader "Mirror/NetworkGraphLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Width ("Width", Int) = 0
        _LineWidth ("Line Width", Float) = 0.005
        _CategoryCount ("CategoryCount", Int) = 0
        _MaxValue ("MaxValue", Float) = 1
        _DataStart ("DataStart", Int) = 0

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex; // we dont use this, but unitys ui library expects the shader to have a texture
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            uint _Width;
            half _LineWidth;
            uint _CategoryCount;
            uint _MaxValue;
            uint _DataStart;
            half _GraphData[64 /* max. 128 points */ * 8 /* max 8 categories */];
            half4 _CategoryColors[8 /* max 8 categories */];

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            // Helper function to calculate the shortest distance from a point (p) to a line segment (from a to b)
            float distanceToLineSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float2 ap = p - a;
                float t = saturate(dot(ap, ab) / dot(ab, ab));
                // Clamp t between 0 and 1 to ensure we stay within the segment
                float2 closestPoint = a + t * ab; // Find the closest point on the line segment
                return length(p - closestPoint); // Return the distance from p to the closest point on the line
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                uint wCur = (uint)(IN.texcoord.x * _Width);
                uint wMin = wCur == 0 ? 0 : wCur - 1;
                uint wMax = wCur == _Width - 1 ? wCur : wCur + 1;
                float2 screenSize = _ScreenParams.xy;
                // this scaling only works if the object is flat and not rotated - but thats fine
                float2 pixelScale = float2(1 / ddx(IN.texcoord.x), 1 / ddy(IN.texcoord.y));
                float2 screenSpaceUV = IN.texcoord * pixelScale;
                half4 color = half4(0, 0, 0, 0);
                // Loop through the graph's points
                bool colored = false;
                for (uint wNonOffset = wMin; wNonOffset < wMax && !colored; wNonOffset++)
                {
                    uint w = (wNonOffset + _DataStart) % _Width;
                    // previous entry, unless it's the start, then we clamp to start
                    uint nextW = (w + 1) % _Width;

                    float texPosCurrentX = float(wNonOffset) / _Width;
                    float texPosPrevX = texPosCurrentX + 1.0f / _Width;


                    for (uint c = 0; c < _CategoryCount; c++)
                    {
                        float categoryValueCurrent = _GraphData[w * _CategoryCount + c] / _MaxValue;
                        float categoryValueNext = _GraphData[nextW * _CategoryCount + c] / _MaxValue;

                        float2 pointCurrent = float2(texPosCurrentX, categoryValueCurrent);
                        float2 pointNext = float2(texPosPrevX, categoryValueNext);

                        float distance = distanceToLineSegment(screenSpaceUV, pointCurrent * pixelScale,
                                                               pointNext * pixelScale);

                        if (distance < _LineWidth)
                        {
                            color = _CategoryColors[c];
                            colored = true;
                            break;
                        }
                    }
                }

                color *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
    color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
    clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}