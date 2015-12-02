using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Armor
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        float cannonCooldown;
        float waterHeight;
        HeightMapInfo heightMapInfo;
        float[,] heightData;
        VertexBuffer terrainVertexBuffer;
        IndexBuffer terrainIndexBuffer;
        int[] terrainIndices;
        int terrainWidth;
        int terrainLength;
        VertexMultitextured[] terrainVertices;
        SpriteFont spritefont;
        String testString = "UI";

        Matrix reflectionViewMatrix;
        Texture2D waterBumpMap;
        VertexBuffer waterVertexBuffer;

        Texture2D tex_sand, tex_grass, tex_rock, tex_snow;
        Effect effect;
        Matrix viewMatrix, projectionMatrix;

        Vector3 cameraPosition = new Vector3(50, 30, 50);
        float leftrightRot = 0;//MathHelper.PiOver2;
        float updownRot = 0;//-MathHelper.Pi / 10.0f;
        const float rotationSpeed = 0.3f;
        const float moveSpeed = 30.0f;

        private MouseState originalMouseState;

        RenderTarget2D refractionRenderTarget;
        Texture2D refractionMap;
        RenderTarget2D reflectionRenderTarget;
        Texture2D reflectionMap;

        Tank tank;
        LinkedList<Shell> shells;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 600;
            graphics.PreferredBackBufferHeight = 450;
            graphics.IsFullScreen = false;
            graphics.ApplyChanges();
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }
        #region Load    ---------------------------------------------------------------
        protected override void LoadContent()
        {
            Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            originalMouseState = Mouse.GetState();

            spriteBatch = new SpriteBatch(GraphicsDevice);
            LoadPipeline();
            SetUpLighting();
            SetUpTerrain();

            Tank.LoadContent(Content);
            tank = new Tank();
            tank.position = new Vector3(50, 5, 50);

            Shell.LoadContent(Content, GraphicsDevice, effect);
            shells = new LinkedList<Shell>();

            SetUpCamera();

            int i = 0;
            foreach (ModelMesh mesh in Tank.model.Meshes)
                foreach (BasicEffect currentEffect in mesh.Effects)
                    Tank.textures[i++] = currentEffect.Texture;
            foreach (ModelMesh mesh in Tank.model.Meshes)
                foreach (ModelMeshPart meshPart in mesh.MeshParts)
                    meshPart.Effect = effect;

            PresentationParameters pp = GraphicsDevice.PresentationParameters;
            refractionRenderTarget = new RenderTarget2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, true, GraphicsDevice.DisplayMode.Format, DepthFormat.Depth16);
            reflectionRenderTarget = new RenderTarget2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, true, GraphicsDevice.DisplayMode.Format, DepthFormat.Depth16);
        }
        void LoadPipeline()
        {
            effect = Content.Load<Effect>("Shaders");
            tex_sand = Content.Load<Texture2D>("sand");
            tex_grass = Content.Load<Texture2D>("grass");
            tex_rock = Content.Load<Texture2D>("rock");
            tex_snow = Content.Load<Texture2D>("snow");
            waterBumpMap = Content.Load<Texture2D>("waterbump");
            spritefont = Content.Load<SpriteFont>("SpriteFont1");
        }
        private void SetUpLighting()
        {
            Vector3 lightDirection = new Vector3(-0.5f, -0.5f, -0.5f);
            lightDirection.Normalize();
            effect.Parameters["xLightDirection"].SetValue(lightDirection);
            effect.Parameters["xAmbient"].SetValue(0.4f);
            effect.Parameters["xEnableLighting"].SetValue(true);
        }
        void SetUpCamera()
        {
            UpdateViewMatrix();
            projectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 1.0f, 300.0f);
        }
        void SetUpTerrain()
        {
            #region Loading Height Data
            Texture2D heightMap = Content.Load<Texture2D>("Land");
            float minimumHeight = float.MaxValue;
            float maximumHeight = float.MinValue;
            
            float heightRange = 20;
            waterHeight = heightRange * 0.10f;

            terrainWidth = heightMap.Width;
            terrainLength = heightMap.Height;

            Color[] heightMapColors = new Color[terrainWidth * terrainLength];
            heightMap.GetData(heightMapColors);

            heightData = new float[terrainWidth, terrainLength];
            for (int x = 0; x < terrainWidth; x++)
                for (int y = 0; y < terrainLength; y++)
                {
                    heightData[x, y] = heightMapColors[x + y * terrainWidth].R;
                    if (heightData[x, y] < minimumHeight) minimumHeight = heightData[x, y];
                    if (heightData[x, y] > maximumHeight) maximumHeight = heightData[x, y];
                }

            for (int x = 0; x < terrainWidth; x++)
                for (int y = 0; y < terrainLength; y++)
                    heightData[x, y] = (heightData[x, y] - minimumHeight) / (maximumHeight - minimumHeight) * heightRange;
            #endregion
            #region Generating Vertices
            terrainVertices = new VertexMultitextured[terrainWidth * terrainLength];

            for (int x = 0; x < terrainWidth; x++)
            {
                for (int y = 0; y < terrainLength; y++)
                {
                    float height = heightData[x, y];
                    terrainVertices[x + y * terrainWidth].Position = new Vector3(x, height, y);
                    terrainVertices[x + y * terrainWidth].TextureCoordinate.X = (float)x / 30.0f;
                    terrainVertices[x + y * terrainWidth].TextureCoordinate.Y = (float)y / 30.0f;
                    terrainVertices[x + y * terrainWidth].TexWeights.X = MathHelper.Clamp(1.0f - Math.Abs(height - 0) / (heightRange / 4), 0, 1);//sand
                    terrainVertices[x + y * terrainWidth].TexWeights.Y = MathHelper.Clamp(1.0f - Math.Abs(height - heightRange / 3) / (heightRange / 4), 0, 1);//grass
                    terrainVertices[x + y * terrainWidth].TexWeights.Z = MathHelper.Clamp(1.0f - Math.Abs(height - 2 * heightRange / 3) / (heightRange / 4), 0, 1);//rock
                    terrainVertices[x + y * terrainWidth].TexWeights.W = MathHelper.Clamp(1.0f - Math.Abs(height - heightRange) / (heightRange / 4), 0, 1);//snow

                    float total = terrainVertices[x + y * terrainWidth].TexWeights.X;
                    total += terrainVertices[x + y * terrainWidth].TexWeights.Y;
                    total += terrainVertices[x + y * terrainWidth].TexWeights.Z;
                    total += terrainVertices[x + y * terrainWidth].TexWeights.W;

                    terrainVertices[x + y * terrainWidth].TexWeights.X /= total;
                    terrainVertices[x + y * terrainWidth].TexWeights.Y /= total;
                    terrainVertices[x + y * terrainWidth].TexWeights.Z /= total;
                    terrainVertices[x + y * terrainWidth].TexWeights.W /= total;
                }
            }
            #endregion
            #region Generating Indices
            terrainIndices = new int[(terrainWidth - 1) * (terrainLength - 1) * 6];
            int counter = 0;
            for (int y = 0; y < terrainLength - 1; y++)
            {
                for (int x = 0; x < terrainWidth - 1; x++)
                {
                    int lowerLeft = x + (y + 1) * terrainWidth;
                    int lowerRight = (x + 1) + (y + 1) * terrainWidth;
                    int topLeft = x + y * terrainWidth;
                    int topRight = (x + 1) + y * terrainWidth;

                    terrainIndices[counter++] = topLeft;
                    terrainIndices[counter++] = lowerRight;
                    terrainIndices[counter++] = lowerLeft;

                    terrainIndices[counter++] = topLeft;
                    terrainIndices[counter++] = topRight;
                    terrainIndices[counter++] = lowerRight;
                }
            }
            #endregion
            #region Calculating Normals
            for (int i = 0; i < terrainVertices.Length; i++)
                terrainVertices[i].Normal = new Vector3(0, 0, 0);

            for (int i = 0; i < terrainIndices.Length / 3; i++)
            {
                int index1 = terrainIndices[i * 3];
                int index2 = terrainIndices[i * 3 + 1];
                int index3 = terrainIndices[i * 3 + 2];

                Vector3 side1 = terrainVertices[index1].Position - terrainVertices[index3].Position;
                Vector3 side2 = terrainVertices[index1].Position - terrainVertices[index2].Position;
                Vector3 normal = Vector3.Cross(side1, side2);

                terrainVertices[index1].Normal += normal;
                terrainVertices[index2].Normal += normal;
                terrainVertices[index3].Normal += normal;
            }

            for (int i = 0; i < terrainVertices.Length; i++)
            {
                terrainVertices[i].Normal.Normalize();
                float temp = terrainVertices[i].Normal.Y;
                temp = MathHelper.Clamp((float)Math.Pow(temp, 2), 0, 1);
                float mag = terrainVertices[i].TexWeights.Length();
                terrainVertices[i].TexWeights.Normalize();
                terrainVertices[i].TexWeights *= temp;
                terrainVertices[i].TexWeights += Vector4.UnitZ * (1 - temp);
                terrainVertices[i].TexWeights *= mag;
            }
            #endregion
            #region Loading Buffers
            terrainVertexBuffer = new VertexBuffer(GraphicsDevice, VertexMultitextured.VertexDeclaration, terrainVertices.Length, BufferUsage.WriteOnly);
            terrainVertexBuffer.SetData(terrainVertices);

            terrainIndexBuffer = new IndexBuffer(GraphicsDevice, typeof(int), terrainIndices.Length, BufferUsage.WriteOnly);
            terrainIndexBuffer.SetData(terrainIndices);
            #endregion
            #region Water
            VertexPositionTexture[] waterVertices = new VertexPositionTexture[6];

            waterVertices[0] = new VertexPositionTexture(new Vector3(0, waterHeight, terrainLength), new Vector2(0, 1));
            waterVertices[2] = new VertexPositionTexture(new Vector3(terrainWidth, waterHeight, 0), new Vector2(1, 0));
            waterVertices[1] = new VertexPositionTexture(new Vector3(0, waterHeight, 0), new Vector2(0, 0));

            waterVertices[3] = new VertexPositionTexture(new Vector3(0, waterHeight, terrainLength), new Vector2(0, 1));
            waterVertices[5] = new VertexPositionTexture(new Vector3(terrainWidth, waterHeight, terrainLength), new Vector2(1, 1));
            waterVertices[4] = new VertexPositionTexture(new Vector3(terrainWidth, waterHeight, 0), new Vector2(1, 0));

            waterVertexBuffer = new VertexBuffer(GraphicsDevice, VertexPositionTexture.VertexDeclaration, waterVertices.Length, BufferUsage.WriteOnly);
            waterVertexBuffer.SetData(waterVertices);

            effect.Parameters["xFogColor0"].SetValue(new Vector4(new Vector3(0.7f), 1));
            effect.Parameters["xFogColor1"].SetValue(Color.WhiteSmoke.ToVector4());
            effect.Parameters["xWaterBumpMap"].SetValue(waterBumpMap);
            effect.Parameters["xWaveLength"].SetValue(0.5f);
            effect.Parameters["xWaveHeight"].SetValue(0.1f);
            Vector3 windDirection = new Vector3(1, 0, 0);
            effect.Parameters["xWindForce"].SetValue(7.0f);
            effect.Parameters["xWindDirection"].SetValue(windDirection);
            #endregion
            Vector3[,] normals = new Vector3[terrainWidth, terrainLength];
            for (int x = 0; x < terrainWidth; x++)
                for (int y = 0; y < terrainLength; y++)
                    normals[x, y] = terrainVertices[x + y * terrainWidth].Normal;
            heightMapInfo = new HeightMapInfo(heightData, normals, 1);
        }
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }
        #endregion
        #region Update  ---------------------------------------------------------------
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();
            float Elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            cannonCooldown = Math.Max(0, cannonCooldown - Elapsed);
            ProcessInput(Elapsed);
            
            UpdateViewMatrix();

            float height;
            Vector3 temp;
            LinkedList<Shell> delete = new LinkedList<Shell>();
            foreach (Shell shell in shells)
            {
                shell.Update(Elapsed);
                heightMapInfo.GetHeightAndNormal(shell.position, out height, out temp);
                if (shell.position.Y <= height)
                    delete.AddLast(shell);
            }
            foreach (Shell shell in delete)
            {
                shells.Remove(shell);
            }

            base.Update(gameTime);
        }
        void UpdateViewMatrix()
        {
            Matrix cameraRotation = Matrix.CreateFromAxisAngle(tank.normal, leftrightRot);
            cameraRotation *= Matrix.CreateFromAxisAngle(Vector3.Transform(Vector3.UnitX, cameraRotation), updownRot);
            cameraPosition = tank.position + 4.5f * tank.normal + 5 * Vector3.Transform(Vector3.UnitZ, cameraRotation);

            Vector3 cameraFinalTarget = tank.position + 4f * tank.normal;
            testString = "X: " + tank.normal.X +
                "\nY: " + tank.normal.Y +
                "\nZ: " + tank.normal.Z;

            viewMatrix = Matrix.CreateLookAt(cameraPosition, cameraFinalTarget, tank.normal);

            Vector3 reflCameraPosition = cameraPosition;
            reflCameraPosition.Y = -cameraPosition.Y + waterHeight * 2;
            Vector3 reflTargetPos = cameraFinalTarget;
            reflTargetPos.Y = -cameraFinalTarget.Y + waterHeight * 2;
            Vector3 invUpVector = tank.normal;
            invUpVector.Y *= -1;
            reflectionViewMatrix = Matrix.CreateLookAt(reflCameraPosition, reflTargetPos, invUpVector);
        }
        void ProcessInput(float Elapsed)
        {
            #region Mouse
            MouseState currentMouseState = Mouse.GetState();
            if (currentMouseState != originalMouseState)
            {
                float xDifference = currentMouseState.X - originalMouseState.X;
                float yDifference = currentMouseState.Y - originalMouseState.Y;
                leftrightRot -= rotationSpeed * xDifference * Elapsed;
                updownRot = MathHelper.Clamp(updownRot - rotationSpeed * yDifference * Elapsed, -MathHelper.PiOver4 / 2, MathHelper.PiOver4 / 2);
                leftrightRot %= MathHelper.TwoPi;
                updownRot %= MathHelper.TwoPi;
                Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
                UpdateViewMatrix();
                if (currentMouseState.LeftButton == ButtonState.Pressed)
                {
                    if (cannonCooldown == 0)
                    {
                        cannonCooldown = 1;
                        Vector3 pos = tank.position;
                        pos += 3 * tank.normal;
                        float angle = -tank.FacingDirection + MathHelper.PiOver2 - tank.turretRotationValue;
                        Matrix mat = Matrix.CreateFromAxisAngle(tank.normal, leftrightRot);
                        mat *= Matrix.CreateFromAxisAngle(Vector3.Transform(Vector3.UnitX, mat), updownRot);
                        Shell shell = new Shell(pos, Vector3.Transform(-Vector3.UnitZ * 100, mat), Vector3.Zero);
                        shells.AddLast(shell);
                    }
                }
            }
            #endregion
            #region Keyboard
            KeyboardState keyState = Keyboard.GetState();
            float turn = 0;
            Vector3 move = new Vector3();
            if (keyState.IsKeyDown(Keys.W))
                move.Z += 1;
            if (keyState.IsKeyDown(Keys.S))
                move.Z -= 1;
            if (keyState.IsKeyDown(Keys.A))
                turn += 1;
            if (keyState.IsKeyDown(Keys.D))
                turn -= 1;
            

            if (keyState.IsKeyDown(Keys.Escape))
                this.Exit();
            #endregion
            tank.Update(Elapsed, turn, move, heightMapInfo, new Vector2(leftrightRot + MathHelper.Pi - tank.FacingDirection, -(updownRot)));
        }
        void AddToCameraPosition(Vector3 vectorToAdd)
        {
            Matrix cameraRotation = Matrix.CreateRotationX(updownRot) * Matrix.CreateRotationY(leftrightRot);
            Vector3 rotatedVector = Vector3.Transform(vectorToAdd, cameraRotation);
            cameraPosition += moveSpeed * rotatedVector;
            UpdateViewMatrix();
        }
        #endregion
        #region Draw    ---------------------------------------------------------------
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            DrawReflectionMap();
            DrawRefractionMap();
            GraphicsDevice.Clear(Color.WhiteSmoke);

            DrawWorld(viewMatrix);
            DrawWater((float)gameTime.TotalGameTime.TotalSeconds / 1000);
            DrawUI();

            base.Draw(gameTime);
        }
        void DrawWorld(Matrix vMatrix)
        {
            DrawTerrain(vMatrix);
            tank.Draw(vMatrix, projectionMatrix);
            foreach (Shell shell in shells)
            {
                shell.Draw(vMatrix, projectionMatrix);
            }
        }
        void DrawTerrain(Matrix vMatrix)
        {
            //Matrix worldMatrix = Matrix.CreateTranslation(-terrainWidth / 2.0f, 0, terrainLength / 2.0f) * Matrix.CreateRotationY(angle);

            effect.CurrentTechnique = effect.Techniques["MultiTextured"];
            effect.Parameters["xTexture0"].SetValue(tex_sand);
            effect.Parameters["xTexture1"].SetValue(tex_grass);
            effect.Parameters["xTexture2"].SetValue(tex_rock);
            effect.Parameters["xTexture3"].SetValue(tex_snow);

            effect.Parameters["xView"].SetValue(vMatrix);
            effect.Parameters["xProjection"].SetValue(projectionMatrix);
            effect.Parameters["xWorld"].SetValue(Matrix.Identity);//worldMatrix);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.Indices = terrainIndexBuffer;
                GraphicsDevice.SetVertexBuffer(terrainVertexBuffer);
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, terrainVertices.Length, 0, terrainIndices.Length / 3);
            }
        }
        void DrawRefractionMap()
        {
            Plane refractionPlane = CreatePlane(waterHeight, new Vector3(0, -1, 0), false);
            effect.Parameters["ClipPlane0"].SetValue(new Vector4(refractionPlane.Normal, refractionPlane.D));
            effect.Parameters["Clipping"].SetValue(true);    // Allows the geometry to be clipped for the purpose of creating a refraction map
            GraphicsDevice.SetRenderTarget(refractionRenderTarget);
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(160,143,110), 1.0f, 0);

            DrawWorld(viewMatrix);

            GraphicsDevice.SetRenderTarget(null);
            effect.Parameters["Clipping"].SetValue(false);
            refractionMap = refractionRenderTarget;
        }
        void DrawReflectionMap()
        {
            Plane reflectionPlane = CreatePlane(waterHeight, new Vector3(0, -1, 0), true);
            effect.Parameters["ClipPlane0"].SetValue(new Vector4(reflectionPlane.Normal, reflectionPlane.D));
            effect.Parameters["Clipping"].SetValue(true);    // Allows the geometry to be clipped for the purpose of creating a refraction map
            GraphicsDevice.SetRenderTarget(reflectionRenderTarget);
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.WhiteSmoke, 1.0f, 0);

            DrawWorld(reflectionViewMatrix);

            GraphicsDevice.SetRenderTarget(null);
            effect.Parameters["Clipping"].SetValue(false);
            reflectionMap = reflectionRenderTarget;
        }
        Plane CreatePlane(float height, Vector3 planeNormalDirection, bool clipSide)
        {
            planeNormalDirection.Normalize();
            Vector4 planeCoeffs = new Vector4(planeNormalDirection, height);
            if (clipSide)
                planeCoeffs *= -1;
            Plane finalPlane = new Plane(planeCoeffs);

            return finalPlane;
        }
        void DrawWater(float time)
        {
            effect.CurrentTechnique = effect.Techniques["Water"];
            Matrix worldMatrix = Matrix.Identity;
            effect.Parameters["xWorld"].SetValue(worldMatrix);
            effect.Parameters["xView"].SetValue(viewMatrix);
            effect.Parameters["xReflectionView"].SetValue(reflectionViewMatrix);
            effect.Parameters["xProjection"].SetValue(projectionMatrix);
            effect.Parameters["xReflectionMap"].SetValue(reflectionMap);
            effect.Parameters["xRefractionMap"].SetValue(refractionMap);
            effect.Parameters["xTime"].SetValue(time);
            effect.Parameters["xCamPos"].SetValue(cameraPosition);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.SetVertexBuffer(waterVertexBuffer);
                GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, waterVertexBuffer.VertexCount / 3);
            }
        }
        void DrawUI()
        {
            spriteBatch.Begin();
            spriteBatch.DrawString(spritefont, testString, new Vector2(1), Color.DarkGreen);
            spriteBatch.DrawString(spritefont, testString, Vector2.Zero, Color.Green);
            spriteBatch.End();
        }
        #endregion
    }
    public struct VertexMultitextured
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 TextureCoordinate;
        public Vector4 TexWeights;

        public static int SizeInBytes = (3 + 3 + 4 + 4) * sizeof(float);
        public static VertexElement[] VertexElements = new VertexElement[]
        {
            new VertexElement( 0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0 ),
            new VertexElement( sizeof(float) * 3, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0 ),
            new VertexElement( sizeof(float) * 6, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0 ),
            new VertexElement( sizeof(float) * 10, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1 ),
        };
        public readonly static VertexDeclaration VertexDeclaration = new VertexDeclaration(VertexElements);
    }
}