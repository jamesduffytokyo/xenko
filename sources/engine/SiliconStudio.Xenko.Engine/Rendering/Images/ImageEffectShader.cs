﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections.Generic;

using SiliconStudio.Core;
using SiliconStudio.Xenko.Graphics;

namespace SiliconStudio.Xenko.Rendering.Images
{
    /// <summary>
    /// Post effect using an <see cref="Effect"/> (either xkfx or xksl).
    /// </summary>
    [DataContract("ImageEffectShader")]
    public class ImageEffectShader : ImageEffect
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageEffectShader" /> class.
        /// </summary>
        public ImageEffectShader(string effectName = null)
        {
            EffectName = effectName;
            EffectInstance = new DynamicEffectInstance(EffectName, Parameters);
        }

        /// <inheritdoc/>
        protected override void InitializeCore()
        {
            base.InitializeCore();

            if (EffectName == null) throw new ArgumentNullException("No EffectName specified");

            // Setup the effect compiler
            EffectInstance.Initialize(Context.Services);

            SetDefaultParameters();
        }

        /// <summary>
        /// The current effect instance.
        /// </summary>
        public DynamicEffectInstance EffectInstance { get; private set; }

        /// <summary>
        /// Effect name.
        /// </summary>
        [DataMemberIgnore]
        public string EffectName { get; protected set; }

        /// <summary>
        /// Optional shared parameters. This list must be setup before calling <see cref="Initialize"/>.
        /// </summary>
        [DataMemberIgnore]
        public List<ParameterCollection> SharedParameterCollections { get { throw new InvalidOperationException(); } }

        /// <summary>
        /// Gets the parameter collections used by this effect.
        /// </summary>
        /// <value>The parameter collections.</value>
        [DataMemberIgnore]
        public List<ParameterCollection> ParameterCollections
        {
            get
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Sets the default parameters (called at constructor time and if <see cref="Reset"/> is called)
        /// </summary>
        protected override void SetDefaultParameters()
        {
            // TODO: Do not use slow version
            Parameters.SetResourceSlow(TexturingKeys.Sampler, GraphicsDevice.SamplerStates.LinearClamp);
        }

        protected override void PreDrawCore(RenderContext context)
        {
            base.PreDrawCore(context);

            // Default handler for parameters
            UpdateParameters();
        }

        /// <summary>
        /// Updates the effect <see cref="ImageEffectShader.Parameters" /> from properties defined in this instance. See remarks.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Expecting less than 10 textures in input</exception>
        /// <remarks>By default, all the input textures will be remapped to <see cref="TexturingKeys.Texture0" />...etc.</remarks>
        protected virtual void UpdateParameters()
        {
            // By default, we are copying all input textures to TexturingKeys.Texture#
            var count = InputCount;
            for (int i = 0; i < count; i++)
            {
                var texture = GetInput(i);
                if (i < TexturingKeys.DefaultTextures.Count)
                {
                    var texturingKeys = texture.Dimension == TextureDimension.TextureCube ? TexturingKeys.TextureCubes : TexturingKeys.DefaultTextures;
                    // TODO: Do not use slow version
                    Parameters.SetResourceSlow(texturingKeys[i], texture);
                }
                else
                {
                    throw new InvalidOperationException("Expecting less than {0} textures in input".ToFormat(TexturingKeys.DefaultTextures.Count));
                }
            }
        }

        protected override void DrawCore(RenderContext context)
        {
            // Draw a full screen quad
            GraphicsDevice.DrawQuad(EffectInstance);
        }
    }
}