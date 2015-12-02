using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

namespace Armor
{
    class Shell
    {
        static IndexBuffer indexBuffer;
        static VertexBuffer vertexBuffer;
        public Vector3 position, velocity, rotation;
        static Effect effect;
        static GraphicsDevice gd;

        public static void LoadContent(ContentManager content, GraphicsDevice graphicsDevice, Effect e)
        {
            effect = e;
            gd = graphicsDevice;
            LoadModel(10);
        }
        static void LoadModel(int round)
        {
            int max = 1 + 3 * round;
            VertexPositionColor[] vertices = new VertexPositionColor[2 + 3 * round];//1, 5, 5, 5, 1
            vertices[0] = new VertexPositionColor();
            vertices[0].Position = new Vector3(0,0,-1);
            vertices[max] = new VertexPositionColor();
            vertices[max].Position = new Vector3(0, 0,1);
            for (int i = 0; i < round; i++)
            {
                float angle = i * MathHelper.TwoPi / round;
                vertices[i + 1] = new VertexPositionColor();
                vertices[i + 1].Position = new Vector3(.2f * (float)Math.Cos(angle), .2f * (float)Math.Sin(angle), -.70f);
                vertices[i + 1 + round] = new VertexPositionColor();
                vertices[i + 1 + round].Position = new Vector3(.3f * (float)Math.Cos(angle), .3f * (float)Math.Sin(angle), -.30f);
                vertices[i + 1 + 2 * round] = new VertexPositionColor();
                vertices[i + 1 + 2 * round].Position = new Vector3(.25f * (float)Math.Cos(angle), .25f * (float)Math.Sin(angle), 1);
            }
            for (int i = 0; i < max + 1; i++) vertices[i].Color = Color.Black;
            vertices[0].Color = Color.White;
            for (int i = 1; i < round + 1; i++) vertices[i].Color = Color.Red;
            vertexBuffer = new VertexBuffer(gd, VertexPositionColor.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);
            int[] indices = new int[round * (6 * 3 + 3 * 2)];
            int count = 0;
            #region Nose
            for (int i = 1; i < round; i++)
            {
                indices[count++] = 0;
                indices[count++] = i;
                indices[count++] = i + 1;
            }
            indices[count++] = 0;
            indices[count++] = round;
            indices[count++] = 1;
            #endregion
            #region BackNose
            for (int i = 1; i < round; i++)
            {
                indices[count++] = i;
                indices[count++] = i + round;
                indices[count++] = i + 1;

                indices[count++] = i + 1;
                indices[count++] = i + round;
                indices[count++] = i + round + 1;
            }
            indices[count++] = round;
            indices[count++] = 2 * round;
            indices[count++] = 1;

            indices[count++] = 1;
            indices[count++] = 2 * round;
            indices[count++] = round + 1;
            #endregion
            #region MidBody
            for (int i = round + 1; i < 2 * round; i++)
            {
                indices[count++] = i;
                indices[count++] = i + round;
                indices[count++] = i + 1;

                indices[count++] = i + 1;
                indices[count++] = i + round;
                indices[count++] = i + round + 1;
            }
            indices[count++] = 2 * round;
            indices[count++] = 3 * round;
            indices[count++] = round + 1;

            indices[count++] = round + 1;
            indices[count++] = 3 * round;
            indices[count++] = 2 * round + 1;
            #endregion
            #region Tail
            for (int i = 2 * round; i < 3 * round; i++)
            {
                indices[count++] = max;
                indices[count++] = i + 1;
                indices[count++] = i;
            }
            indices[count++] = max;
            indices[count++] = 2 * round + 1;
            indices[count++] = 3 * round;
            #endregion
            indexBuffer = new IndexBuffer(gd, typeof(int), indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
        }

        public Shell(Vector3 pos, Vector3 vel, Vector3 rot)
        {
            position = pos;
            velocity = vel;
            rotation = rot;
        }

        public void Update(float Elapsed)
        {
            position += velocity * Elapsed;
            velocity += Vector3.UnitY * -9.8f * Elapsed;
            float xzAngle = (float)Math.Atan(-velocity.Z / velocity.X) - MathHelper.PiOver2;
            if (velocity.X < 0)
                xzAngle += MathHelper.Pi;
            rotation.Y = xzAngle;
            float zyAngle = (float)Math.Atan(velocity.Y / new Vector2(velocity.X, velocity.Z).Length());// -MathHelper.PiOver2;
            rotation.X = zyAngle;
        }
        public void Draw(Matrix viewMatrix, Matrix projectionMatrix)
        {
            Matrix worldMatrix = Matrix.CreateScale(.5f) * Matrix.CreateRotationX(rotation.X) * Matrix.CreateRotationY(rotation.Y) * Matrix.CreateRotationZ(rotation.Z) * Matrix.CreateTranslation(position);
            effect.Parameters["xWorld"].SetValue(worldMatrix);
            effect.Parameters["xView"].SetValue(viewMatrix);
            effect.Parameters["xProjection"].SetValue(projectionMatrix);
            effect.CurrentTechnique = effect.Techniques["Colored"];
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                gd.Indices = indexBuffer;
                gd.SetVertexBuffer(vertexBuffer);
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertexBuffer.VertexCount, 0, indexBuffer.IndexCount / 3);
            }
        }
    }
}
