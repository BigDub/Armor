#region File Description
//-----------------------------------------------------------------------------
// Tank.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
#endregion

namespace Armor
{
    class Tank
    {
        #region Constants


        // This constant controls how quickly the tank can move forward and backward
        static float TankVelocity = 10;

        // The radius of the tank's wheels. This is used when we calculate how fast they
        // should be rotating as the tank moves.
        static float TankWheelRadius = 1.8f;

        // controls how quickly the tank can turn from side to side.
        static float TankTurnSpeed = 2;


        #endregion


        #region Properties

        /// <summary>
        /// The position of the tank. The camera will use this value to position itself.
        /// </summary>
        public Vector3 position;

        public Vector3 normal;

        /// <summary>
        /// The direction that the tank is facing, in radians. This value will be used
        /// to position and and aim the camera.
        /// </summary>
        public float FacingDirection
        {
            get { return facingDirection; }
        }
        private float facingDirection;


        #endregion


        #region Fields

        // The tank's model - a fearsome sight.
        public static Model model;

        public static Texture2D[] textures;

        // how is the tank oriented? We'll calculate this based on the user's input and
        // the heightmap's normals, and then use it when drawing.
        Matrix orientation = Matrix.Identity;

        // we'll use this value when making the wheels roll. It's calculated based on 
        // the distance moved.
        Matrix wheelRollMatrix = Matrix.Identity;

        // The Simple Animation Sample at creators.xna.com explains the technique that 
        // we will be using in order to roll the tanks wheels. In this technique, we
        // will keep track of the ModelBones that control the wheels, and will manually
        // set their transforms. These next eight fields will be used for this
        // technique.
        static ModelBone leftBackWheelBone;
        static ModelBone rightBackWheelBone;
        static ModelBone leftFrontWheelBone;
        static ModelBone rightFrontWheelBone;
        static ModelBone leftSteerBone;
        static ModelBone rightSteerBone;
        static ModelBone turretBone;
        static ModelBone cannonBone;
        static ModelBone hatchBone;

        Matrix leftBackWheelTransform;
        Matrix rightBackWheelTransform;
        Matrix leftFrontWheelTransform;
        Matrix rightFrontWheelTransform;
        Matrix leftSteerTransform;
        Matrix rightSteerTransform;
        Matrix turretTransform;
        Matrix cannonTransform;
        Matrix hatchTransform;

        public float wheelRotationValue;
        public float steerRotationValue;
        public float turretRotationValue;
        public float cannonRotationValue;
        public float hatchRotationValue;

        static Matrix[] boneTransforms;
        #endregion


        #region Initialization

        public Tank()
        {
            //Store the original transform matrix for each animating bone.
            leftBackWheelTransform = leftBackWheelBone.Transform;
            rightBackWheelTransform = rightBackWheelBone.Transform;
            leftFrontWheelTransform = leftFrontWheelBone.Transform;
            rightFrontWheelTransform = rightFrontWheelBone.Transform;
            leftSteerTransform = leftSteerBone.Transform;
            rightSteerTransform = rightSteerBone.Transform;
            turretTransform = turretBone.Transform;
            cannonTransform = cannonBone.Transform;
            hatchTransform = hatchBone.Transform;

            normal = Vector3.UnitY;
        }

        /// <summary>
        /// Called when the Game is loading its content. Pass in a ContentManager so the
        /// tank can load its model.
        /// </summary>
        public static void LoadContent(ContentManager content)
        {
            model = content.Load<Model>("Tank");
            textures = new Texture2D[20];
            // as discussed in the Simple Animation Sample, we'll look up the bones
            // that control the wheels.
            leftBackWheelBone = model.Bones["l_back_wheel_geo"];
            rightBackWheelBone = model.Bones["r_back_wheel_geo"];
            leftFrontWheelBone = model.Bones["l_front_wheel_geo"];
            rightFrontWheelBone = model.Bones["r_front_wheel_geo"];
            leftSteerBone = model.Bones["l_steer_geo"];
            rightSteerBone = model.Bones["r_steer_geo"];
            turretBone = model.Bones["turret_geo"];
            cannonBone = model.Bones["canon_geo"];
            hatchBone = model.Bones["hatch_geo"];

            boneTransforms = new Matrix[model.Bones.Count];
        }

        #endregion

        #region Update and Draw

        /// <summary>
        /// This function is called when the game is Updating in response to user input.
        /// It'll move the tank around the heightmap, and update all of the tank's 
        /// necessary state.
        /// </summary>
        public void Update(float elapsed, float turnAmount, Vector3 movement, HeightMapInfo heightMapInfo, Vector2 turretVector)
        {
            facingDirection += turnAmount * TankTurnSpeed * elapsed;

            // next, we'll create a rotation matrix from the direction the tank is 
            // facing, and use it to transform the vector.
            
            Vector3 velocity = Vector3.Transform(movement, orientation);
            velocity *= TankVelocity;

            turretRotationValue = turretVector.X;
            cannonRotationValue = turretVector.Y;

            // Now we know how much the user wants to move. We'll construct a temporary
            // vector, newPosition, which will represent where the user wants to go. If
            // that value is on the heightmap, we'll allow the move.
            Vector3 newPosition = position + velocity * elapsed;
            if (heightMapInfo.IsOnHeightmap(newPosition))
            {
                // now that we know we're on the heightmap, we need to know the correct
                // height and normal at this position.
                heightMapInfo.GetHeightAndNormal(newPosition,
                    out newPosition.Y, out normal);


                // As discussed in the doc, we'll use the normal of the heightmap
                // and our desired forward direction to recalculate our orientation
                // matrix. It's important to normalize, as well.
                

                // now we need to roll the tank's wheels "forward." to do this, we'll
                // calculate how far they have rolled, and from there calculate how much
                // they must have rotated.
                float distanceMoved = Vector3.Distance(position, newPosition);
                float theta = distanceMoved / TankWheelRadius;
                int rollDirection = movement.Z > 0 ? 1 : -1;

                wheelRotationValue += theta * rollDirection;

                // once we've finished all computations, we can set our position to the
                // new position that we calculated.
                position = newPosition;
            }
            orientation = Matrix.CreateRotationY(FacingDirection);
            orientation.Up = normal;

            orientation.Right = Vector3.Cross(orientation.Forward, orientation.Up);
            orientation.Right = Vector3.Normalize(orientation.Right);

            orientation.Forward = Vector3.Cross(orientation.Up, orientation.Right);
            orientation.Forward = Vector3.Normalize(orientation.Forward);
        }

        public void Draw(Matrix viewMatrix, Matrix projectionMatrix)
        {
            // calculate the tank's world matrix, which will be a combination of our
            // orientation and a translation matrix that will put us at at the correct
            // position.
            Matrix worldMatrix = Matrix.CreateScale(0.008f) * orientation * Matrix.CreateTranslation(position);
            //model.Root.Transform = worldMatrix;

            // Calculate matrices based on the current animation position.
            Matrix wheelRotation = Matrix.CreateRotationX(wheelRotationValue);
            Matrix steerRotation = Matrix.CreateRotationY(steerRotationValue);
            Matrix turretRotation = Matrix.CreateRotationY(turretRotationValue);
            Matrix cannonRotation = Matrix.CreateRotationX(cannonRotationValue);
            Matrix hatchRotation = Matrix.CreateRotationX(hatchRotationValue);

            // Apply matrices to the relevant bones.
            leftBackWheelBone.Transform = wheelRotation * leftBackWheelTransform;
            rightBackWheelBone.Transform = wheelRotation * rightBackWheelTransform;
            leftFrontWheelBone.Transform = wheelRotation * leftFrontWheelTransform;
            rightFrontWheelBone.Transform = wheelRotation * rightFrontWheelTransform;
            leftSteerBone.Transform = steerRotation * leftSteerTransform;
            rightSteerBone.Transform = steerRotation * rightSteerTransform;
            turretBone.Transform = turretRotation * turretTransform;
            cannonBone.Transform = cannonRotation * cannonTransform;
            hatchBone.Transform = hatchRotation * hatchTransform;

            // now that we've updated the wheels' transforms, we can create an array
            // of absolute transforms for all of the bones, and then use it to draw.
            Matrix[] boneTransforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);

            
            int i = 0;
            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (Effect effect in mesh.Effects)
                {
                    effect.Parameters["xWorld"].SetValue(boneTransforms[mesh.ParentBone.Index] * worldMatrix);
                    effect.Parameters["xView"].SetValue(viewMatrix);
                    effect.Parameters["xProjection"].SetValue(projectionMatrix);
                    effect.Parameters["xTexture"].SetValue(textures[i++]);
                    effect.CurrentTechnique = effect.Techniques["Textured"];
                }
                mesh.Draw();
            }
        }

        #endregion
    }
}
