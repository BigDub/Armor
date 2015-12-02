float4x4 xView;
float4x4 xProjection;
float4x4 xWorld;
float3 xLightDirection;
float xAmbient;
bool xEnableLighting;
float3 xCamPos;
float xTime;
float3 xWindDirection;
float xWindForce;
bool Clipping;
float4 ClipPlane0;
float4 xFogColor0;
float4 xFogColor1;
float xWaveLength;
float xWaveHeight;
float4x4 xReflectionView;

//------- Texture Samplers --------
Texture xTexture;
sampler TextureSampler = sampler_state { texture = <xTexture> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};Texture xTexture0;
sampler TextureSampler0 = sampler_state { texture = <xTexture0> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = wrap; AddressV = wrap;};Texture xTexture1;
sampler TextureSampler1 = sampler_state { texture = <xTexture1> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = wrap; AddressV = wrap;};Texture xTexture2;
sampler TextureSampler2 = sampler_state { texture = <xTexture2> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};Texture xTexture3;
sampler TextureSampler3 = sampler_state { texture = <xTexture3> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};

Texture xReflectionMap;
Texture xRefractionMap;
Texture xWaterBumpMap;
sampler ReflectionSampler = sampler_state { texture = <xReflectionMap> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};
sampler RefractionSampler = sampler_state { texture = <xRefractionMap> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};
sampler WaterBumpMapSampler = sampler_state { texture = <xWaterBumpMap> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};

struct PixelToFrame
{
    float4 Color : COLOR0;
};

//------- Technique: Multitextured ---
struct MTVertexToPixel
{
    float4 Position         : POSITION;    
    float4 Color            : COLOR0;
    float3 Normal            : TEXCOORD0;
    float2 TextureCoords    : TEXCOORD1;
    float4 LightDirection    : TEXCOORD2;
    float4 TextureWeights    : TEXCOORD3;
	float4 Position3D		: TEXCOORD6;
	float Depth            : TEXCOORD4;

	float4 clipDistances     : TEXCOORD5;   //MSS - Water Refactor added
};

MTVertexToPixel MultiTexturedVS( float4 inPos : POSITION, float3 inNormal: NORMAL, float2 inTexCoords: TEXCOORD0, float4 inTexWeights: TEXCOORD1)
{    
    MTVertexToPixel Output = (MTVertexToPixel)0;
    float4x4 preViewProjection = mul (xView, xProjection);
    float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);
    
    Output.Position = mul(inPos, preWorldViewProjection);
    Output.Normal = mul(normalize(inNormal), xWorld);
    Output.TextureCoords = inTexCoords;
    Output.LightDirection.xyz = -xLightDirection;
    Output.LightDirection.w = 1;
    Output.TextureWeights = inTexWeights;

	Output.Depth = Output.Position.z/Output.Position.w;
	Output.Position3D = mul(inPos, xWorld);

	Output.clipDistances = dot(inPos, ClipPlane0); //MSS - Water Refactor added
    
    return Output;    
}

PixelToFrame MultiTexturedPS(MTVertexToPixel PSIn)
{
	if (Clipping)  clip(PSIn.clipDistances);  //MSS - Water Refactor added
    PixelToFrame Output = (PixelToFrame)0; 
	
	float dist = length(xCamPos - PSIn.Position3D);
    
	if(dist < 100)
	{
		float lightingFactor = 1;
		if (xEnableLighting)
	        lightingFactor = saturate(saturate(dot(PSIn.Normal, PSIn.LightDirection)) + xAmbient);
	
	    /*float blendDistance = .98f;
	    float blendWidth = 0.01f;
	    float blendFactor = clamp((PSIn.Depth-blendDistance)/blendWidth, 0, 1);
         
		float4 farColor;
		farColor = tex2D(TextureSampler0, PSIn.TextureCoords)*PSIn.TextureWeights.x;
		farColor += tex2D(TextureSampler1, PSIn.TextureCoords)*PSIn.TextureWeights.y;
		farColor += tex2D(TextureSampler2, PSIn.TextureCoords)*PSIn.TextureWeights.z;
		farColor += tex2D(TextureSampler3, PSIn.TextureCoords)*PSIn.TextureWeights.w;*/
	     
	    //float4 nearColor;
	    float2 nearTextureCoords = PSIn.TextureCoords*3;
	    Output.Color = tex2D(TextureSampler0, nearTextureCoords)*PSIn.TextureWeights.x;
	    Output.Color += tex2D(TextureSampler1, nearTextureCoords)*PSIn.TextureWeights.y;
	    Output.Color += tex2D(TextureSampler2, nearTextureCoords)*PSIn.TextureWeights.z;
	    Output.Color += tex2D(TextureSampler3, nearTextureCoords)*PSIn.TextureWeights.w;
	 
	    //Output.Color = lerp(nearColor, farColor, blendFactor);
		Output.Color *= lightingFactor;

		Output.Color = lerp(Output.Color, xFogColor0, max(min( (dist - 60) / 40, 1 ), 0));
	}
	else
	{
		Output.Color = lerp(xFogColor0, xFogColor1, max(min( (dist - 100) / 200, 1 ), 0));
	}
	
	Output.Color.a = 1;
	return Output;
}

technique MultiTextured
{
    pass Pass0
    {
        VertexShader = compile vs_2_0 MultiTexturedVS();
        PixelShader = compile ps_2_0 MultiTexturedPS();
    }
}

//------- Technique: Water --------
struct WVertexToPixel
{
    float4 Position                 : POSITION;
    float4 ReflectionMapSamplingPos    : TEXCOORD1;
    float2 BumpMapSamplingPos        : TEXCOORD2;
    float4 RefractionMapSamplingPos : TEXCOORD3;
    float4 Position3D                : TEXCOORD4;
};

WVertexToPixel WaterVS(float4 inPos : POSITION, float2 inTex: TEXCOORD)
{    
    WVertexToPixel Output = (WVertexToPixel)0;

    float4x4 preViewProjection = mul (xView, xProjection);
    float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);
    float4x4 preReflectionViewProjection = mul (xReflectionView, xProjection);
    float4x4 preWorldReflectionViewProjection = mul (xWorld, preReflectionViewProjection);

    Output.Position = mul(inPos, preWorldViewProjection);
    Output.ReflectionMapSamplingPos = mul(inPos, preWorldReflectionViewProjection);
	Output.BumpMapSamplingPos = inTex/xWaveLength;
	Output.RefractionMapSamplingPos = mul(inPos, preWorldViewProjection);
	Output.Position3D = mul(inPos, xWorld);
	
	float3 windDir = normalize(xWindDirection);    
	float3 perpDir = cross(xWindDirection, float3(0,1,0));
	float ydot = dot(inTex, xWindDirection.xz);
	float xdot = dot(inTex, perpDir.xz);
	float2 moveVector = float2(xdot, ydot);
	moveVector.y += xTime*xWindForce;    
	Output.BumpMapSamplingPos = moveVector/xWaveLength; 

    return Output;
}

PixelToFrame WaterPS(WVertexToPixel PSIn)
{
    PixelToFrame Output = (PixelToFrame)0;
	   
	float dist = length(xCamPos - PSIn.Position3D);
    
	if(dist < 100)
	{
		float2 ProjectedTexCoords;
		ProjectedTexCoords.x = PSIn.ReflectionMapSamplingPos.x/PSIn.ReflectionMapSamplingPos.w/2.0f + 0.5f;
		ProjectedTexCoords.y = -PSIn.ReflectionMapSamplingPos.y/PSIn.ReflectionMapSamplingPos.w/2.0f + 0.5f;
	
		float4 bumpColor = tex2D(WaterBumpMapSampler, PSIn.BumpMapSamplingPos);
		float2 perturbation = xWaveHeight*(bumpColor.rg - 0.5f)*2.0f;
		float2 perturbatedTexCoords = ProjectedTexCoords + perturbation;

		float4 reflectiveColor = tex2D(ReflectionSampler, perturbatedTexCoords);

		float2 ProjectedRefrTexCoords;
		ProjectedRefrTexCoords.x = PSIn.RefractionMapSamplingPos.x/PSIn.RefractionMapSamplingPos.w/2.0f + 0.5f;
		ProjectedRefrTexCoords.y = -PSIn.RefractionMapSamplingPos.y/PSIn.RefractionMapSamplingPos.w/2.0f + 0.5f;    
		float2 perturbatedRefrTexCoords = ProjectedRefrTexCoords + perturbation;
	 
		float4 refractiveColor = tex2D(RefractionSampler, perturbatedRefrTexCoords);

		float3 eyeVector = normalize(xCamPos - PSIn.Position3D);
		float3 normalVector = (bumpColor.rbg-0.5f)*2.0f;
		float fresnelTerm = dot(eyeVector, normalVector);

		Output.Color = lerp(reflectiveColor, refractiveColor, fresnelTerm);
	
		float4 combinedColor = lerp(reflectiveColor, refractiveColor, fresnelTerm);
		float4 dullColor = float4(0.63f, 0.56f, 0.43f, 1);//float4(0.73f, 0.64f, 0.46f, 1.0f);
		Output.Color = lerp(combinedColor, dullColor, 0.1f);

		float3 reflectionVector = -reflect(xLightDirection, normalVector);
		float specular = dot(normalize(reflectionVector), normalize(eyeVector));
		specular = pow(specular, 256);        
		Output.Color.rgb += specular;

		Output.Color = lerp(Output.Color, xFogColor0, max(min( (dist - 60) / 40, 1 ), 0));
	}
	else
	{
		Output.Color = lerp(xFogColor0, xFogColor1, max(min( (dist - 100) / 200, 1 ), 0));
	}

	Output.Color.a = 1;
    return Output;
}

technique Water
{
    pass Pass0
    {
        VertexShader = compile vs_2_0 WaterVS();
        PixelShader = compile ps_2_0 WaterPS();
    }
}

//------- Technique: Textured ---
struct TVertexToPixel
{
    float4 Position         : POSITION;    
    float4 Color            : COLOR0;
    float3 Normal            : TEXCOORD0;
    float2 TextureCoords    : TEXCOORD1;
    float4 LightDirection    : TEXCOORD2;
	float4 Position3D		: TEXCOORD6;

	float4 clipDistances     : TEXCOORD5;   //MSS - Water Refactor added
};

TVertexToPixel TexturedVS( float4 inPos : POSITION, float3 inNormal: NORMAL, float2 inTexCoords: TEXCOORD0)
{    
    TVertexToPixel Output = (TVertexToPixel)0;
    float4x4 preViewProjection = mul (xView, xProjection);
    float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);
    
    Output.Position = mul(inPos, preWorldViewProjection);
    Output.Normal = mul(normalize(inNormal), xWorld);
    Output.TextureCoords = inTexCoords;
    Output.LightDirection.xyz = -xLightDirection;
    Output.LightDirection.w = 1;

	Output.Position3D = mul(inPos, xWorld);

	Output.clipDistances = dot(mul(inPos, xWorld), ClipPlane0); //MSS - Water Refactor added
    
    return Output;    
}

PixelToFrame TexturedPS(MTVertexToPixel PSIn)
{
	if (Clipping)  clip(PSIn.clipDistances);  //MSS - Water Refactor added
    PixelToFrame Output = (PixelToFrame)0; 
	
	float dist = length(xCamPos - PSIn.Position3D);
    
	if(dist < 100)
	{
		float lightingFactor = 1;
		if (xEnableLighting)
	        lightingFactor = saturate(saturate(dot(PSIn.Normal, PSIn.LightDirection)) + xAmbient);
	     
	    Output.Color = tex2D(TextureSampler, PSIn.TextureCoords);
		Output.Color *= lightingFactor;

		Output.Color = lerp(Output.Color, xFogColor0, max(min( (dist - 60) / 40, 1 ), 0));
	}
	else
	{
		Output.Color = lerp(xFogColor0, xFogColor1, max(min( (dist - 100) / 200, 1 ), 0));
	}
	
	Output.Color.a = 1;
	return Output;
}

technique Textured
{
    pass Pass0
    {
        VertexShader = compile vs_2_0 TexturedVS();
        PixelShader = compile ps_2_0 TexturedPS();
    }
}

//------- Technique: Colored ---
struct CVertexToPixel
{
    float4 Position         : POSITION;    
    float4 Color            : COLOR;
	float4 Position3D		: TEXCOORD6;

	float4 clipDistances     : TEXCOORD5;   //MSS - Water Refactor added
};

CVertexToPixel ColoredVS( float4 inPos : POSITION, float4 inColor : COLOR)
{    
    CVertexToPixel Output = (CVertexToPixel)0;
    float4x4 preViewProjection = mul (xView, xProjection);
    float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);

	Output.Color = inColor;
    
    Output.Position = mul(inPos, preWorldViewProjection);

	Output.Position3D = mul(inPos, xWorld);

	Output.clipDistances = dot(mul(inPos, xWorld), ClipPlane0); //MSS - Water Refactor added
    
    return Output;
}

PixelToFrame ColoredPS(CVertexToPixel PSIn)
{
	if (Clipping)  clip(PSIn.clipDistances);  //MSS - Water Refactor added
    PixelToFrame Output = (PixelToFrame)0; 
	
	float dist = length(xCamPos - PSIn.Position3D);
    
	if(dist < 100)
	{	     
		Output.Color = lerp(PSIn.Color, xFogColor0, max(min( (dist - 60) / 40, 1 ), 0));
	}
	else
	{
		Output.Color = lerp(xFogColor0, xFogColor1, max(min( (dist - 100) / 200, 1 ), 0));
	}
	
	Output.Color.a = 1;
	return Output;
}

technique Colored
{
    pass Pass0
    {
        VertexShader = compile vs_2_0 ColoredVS();
        PixelShader = compile ps_2_0 ColoredPS();
    }
}