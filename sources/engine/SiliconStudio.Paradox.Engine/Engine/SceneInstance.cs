﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.Reflection;

using SiliconStudio.Core;
using SiliconStudio.Core.Diagnostics;
using SiliconStudio.Paradox.Effects;
using SiliconStudio.Paradox.Engine.Graphics;
using SiliconStudio.Paradox.Engine.Graphics.Composers;
using SiliconStudio.Paradox.EntityModel;
using SiliconStudio.Paradox.Games;

namespace SiliconStudio.Paradox.Engine
{
    /// <summary>
    /// Manage a collection of entities within a <see cref="Scene"/>.
    /// </summary>
    public sealed class SceneInstance : EntityManager
    {
        private static readonly Logger Log = GlobalLogger.GetLogger("SceneInstance");

        /// <summary>
        /// A property key to get the current scene from the <see cref="RenderContext.Tags"/>.
        /// </summary>
        public static readonly PropertyKey<SceneInstance> Current = new PropertyKey<SceneInstance>("SceneInstance.Current", typeof(SceneInstance));

        private bool enableScripting = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityManager" /> class.
        /// </summary>
        /// <param name="registry">The registry.</param>
        public SceneInstance(IServiceRegistry registry) : this(registry, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneInstance" /> class.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="sceneEntityRoot">The scene entity root.</param>
        /// <param name="enableScripting">if set to <c>true</c> [enable scripting].</param>
        /// <exception cref="System.ArgumentNullException">services
        /// or
        /// sceneEntityRoot</exception>
        public SceneInstance(IServiceRegistry services, Scene sceneEntityRoot, bool enableScripting = true) : base(services)
        {
            if (services == null) throw new ArgumentNullException("services");

            this.enableScripting = enableScripting;
            Scene = sceneEntityRoot;
            RendererTypes = new EntityComponentRendererTypeCollection();
            Load();
        }

        /// <summary>
        /// Gets the scene.
        /// </summary>
        /// <value>The scene.</value>
        public Scene Scene { get; private set; }

        /// <summary>
        /// Gets the component renderers.
        /// </summary>
        /// <value>The renderers.</value>
        private EntityComponentRendererTypeCollection RendererTypes { get; set; }

        protected override void Destroy()
        {
            // TODO: Dispose of Scene, graphics compositor...etc.

            Reset();
            base.Destroy();
        }

        /// <summary>
        /// Draws this scene instance with the specified context and <see cref="RenderFrame"/>.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="toFrame">To frame.</param>
        /// <param name="compositorOverride">The compositor overload. Set this value to by-pass the default compositor of a scene.</param>
        /// <exception cref="System.ArgumentNullException">
        /// context
        /// or
        /// toFrame
        /// </exception>
        public void Draw(RenderContext context, RenderFrame toFrame, ISceneGraphicsCompositor compositorOverride = null)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (toFrame == null) throw new ArgumentNullException("toFrame");

            // If no scene, then we can return immediately
            if (Scene == null)
            {
                return;
            }

            var graphicsDevice = context.GraphicsDevice;

            bool hasGraphicsBegin = false;

            // Update global time
            var gameTime = context.Time;
            context.GraphicsDevice.Parameters.Set(GlobalKeys.Time, (float)gameTime.Total.TotalSeconds);
            context.GraphicsDevice.Parameters.Set(GlobalKeys.TimeStep, (float)gameTime.Elapsed.TotalSeconds);

            try
            {
                graphicsDevice.Begin();
                hasGraphicsBegin = true;

                // Always clear the state of the GraphicsDevice to make sure a scene doesn't start with a wrong setup 
                graphicsDevice.ClearState();

                // Draw the main scene using the current compositor (or the provided override)
                var graphicsCompositor = compositorOverride ?? this.Scene.Settings.GraphicsCompositor;
                if (graphicsCompositor != null)
                {
                    // Push context (pop after using)
                    using (var t1 = context.PushTagAndRestore(RenderFrame.Current, toFrame))
                    using (var t2 = context.PushTagAndRestore(SceneGraphicsLayer.Master, toFrame))
                    using (var t3 = context.PushTagAndRestore(Current, this))
                    using (var t4 = context.PushTagAndRestore(CameraRendererMode.RendererTypesKey, this.RendererTypes))
                    {
                        graphicsCompositor.Draw(context);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("An exception occurred while rendering", ex);
            }
            finally
            {
                if (hasGraphicsBegin)
                {
                    graphicsDevice.End();
                }
            }
        }

        private void Load()
        {
            // If Scene is null, early exit
            if (Scene == null)
            {
                return;
            }

            RendererTypes.Clear();

            // Initialize processors
            if (enableScripting)
                Processors.Add(new ScriptProcessor());
            Processors.Add(new SceneProcessor(this));
            Processors.Add(new HierarchicalProcessor()); // Important to pre-register this processor
            Processors.Add(new TransformProcessor());
            Processors.Add(new CameraProcessor()); // By default, as a scene without a camera is not really possible
            Add(Scene);

            // TODO: RendererTypes could be done outside this instance.
            HandleRendererTypes();
        }

        private void HandleRendererTypes()
        {
            foreach (var componentType in ComponentTypes)
            {
                EntitySystemOnComponentTypeAdded(null, componentType);
            }

            // Make sure that we always have a camera component registered
            RendererTypes.Add(new EntityComponentRendererType(typeof(CameraComponent), typeof(CameraComponentRenderer), int.MinValue));

            ComponentTypeAdded += EntitySystemOnComponentTypeAdded;
        }

        private void EntitySystemOnComponentTypeAdded(object sender, Type type)
        {
            var rendererTypeAttribute = type.GetTypeInfo().GetCustomAttribute<DefaultEntityComponentRendererAttribute>();
            if (rendererTypeAttribute == null)
            {
                return;
            }
            var renderType = Type.GetType(rendererTypeAttribute.TypeName);
            if (renderType != null && typeof(IEntityComponentRenderer).GetTypeInfo().IsAssignableFrom(renderType.GetTypeInfo()) && renderType.GetTypeInfo().GetConstructor(Type.EmptyTypes) != null)
            {
                var entityComponentRendererType = new EntityComponentRendererType(type, renderType, rendererTypeAttribute.Order);
                RendererTypes.Add(entityComponentRendererType);
            }
        }
    }
}