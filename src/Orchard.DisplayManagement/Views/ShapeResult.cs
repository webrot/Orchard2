﻿using Orchard.DisplayManagement.Handlers;
using Orchard.DisplayManagement.Shapes;
using Orchard.Environment.Cache.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orchard.DisplayManagement.Views
{
    public class ShapeResult : IDisplayResult
    {
        private string _defaultLocation;
        private IDictionary<string,string> _otherLocations;
        private string _differentiator;
        private string _prefix;
        private string _cacheId;
        private readonly string _shapeType;
        private readonly Func<IBuildShapeContext, dynamic> _shapeBuilder;
        private readonly Func<dynamic, Task> _processing;
        private Action<CacheContext> _cache;
        private string _groupId;

        public ShapeResult(string shapeType, Func<IBuildShapeContext, dynamic> shapeBuilder)
            :this(shapeType, shapeBuilder, null)
        {
        }

        public ShapeResult(string shapeType, Func<IBuildShapeContext, dynamic> shapeBuilder, Func<dynamic, Task> processing)
        {
            // The shape type is necessary before the shape is created as it will drive the placement
            // resolution which itself can prevent the shape from being created.

            _shapeType = shapeType;
            _shapeBuilder = shapeBuilder;
            _processing = processing;
        }

        public void Apply(BuildDisplayContext context)
        {
            ApplyImplementation(context, context.DisplayType);
        }

        public void Apply(BuildEditorContext context)
        {
            ApplyImplementation(context, null);
        }

        private void ApplyImplementation(BuildShapeContext context, string displayType)
        {
            
            // Look into specific implementations of placements (like placement.info files)
            var placement = context.FindPlacement(_shapeType, _differentiator, displayType);

            // If no placement is found, use the default location
            if (placement == null)
            {
                // Look for mapped display type locations
                if (_otherLocations != null)
                {
                    _otherLocations.TryGetValue(displayType, out _defaultLocation);
                }

                placement = new Descriptors.PlacementInfo() { Location = _defaultLocation };
            }

            // If there are no placement or it's explicitely noop then stop rendering execution
            if (String.IsNullOrEmpty(placement.Location) || placement.Location == "-")
            {
                return;
            }

            // Parse group placement.
            _groupId = placement.GetGroup();

            // If the shape's group doesn't match the currently rendered one, return
            if (!String.Equals(context.GroupId ?? "", _groupId ?? "", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var newShape = _shapeBuilder(context);

            // Ignore it if the driver returned a null shape.
            if (newShape == null)
            {
                return;
            }
            
            ShapeMetadata newShapeMetadata = newShape.Metadata;
            newShapeMetadata.Prefix = _prefix;
            newShapeMetadata.DisplayType = displayType;
            newShapeMetadata.PlacementSource = placement.Source;
            newShapeMetadata.Tab = placement.GetTab();

            // The _processing callback is used to delay execution of costly initialization
            // that can be prevented by caching
            if(_processing != null)
            {
                newShapeMetadata.OnProcessing(_processing);
            }

            // Apply cache settings
            if(!String.IsNullOrEmpty(_cacheId) && _cache != null)
            {
                _cache(newShapeMetadata.Cache(_cacheId));
            }

            // If a specific shape is provided, remove all previous alternates and wrappers.
            if (!String.IsNullOrEmpty(placement.ShapeType))
            {
                newShapeMetadata.Type = placement.ShapeType;
                newShapeMetadata.Alternates.Clear();
                newShapeMetadata.Wrappers.Clear();
            }

            foreach (var alternate in placement.Alternates)
            {
                newShapeMetadata.Alternates.Add(alternate);
            }

            foreach (var wrapper in placement.Wrappers)
            {
                newShapeMetadata.Wrappers.Add(wrapper);
            }

            dynamic parentShape = context.Shape;

            if(placement.IsLayoutZone())
            {
                parentShape = context.Layout;
            }

            var position = placement.GetPosition();
            var zones = placement.GetZones();

            foreach(var zone in zones)
            {
                parentShape = parentShape.Zones[zone];
            }

            if (String.IsNullOrEmpty(position))
            {
                parentShape.Add(newShape);
            }
            else
            {
                parentShape.Add(newShape, position);
            }
        }
        
        /// <summary>
        /// Sets the prefix of the form elements rendered in the shape.
        /// </summary>
        /// <remarks>
        /// The goal is to isolate each shape in case several ones of the same
        /// type are rendered in a view.
        /// </remarks>
        public ShapeResult Prefix(string prefix)
        {
            _prefix = prefix;
            return this;
        }

        /// <summary>
        /// Sets the default location of the shape when no specific placement applies.
        /// </summary>
        public ShapeResult Location(string location)
        {
            _defaultLocation = location;
            return this;
        }

        /// <summary>
        /// Sets the location to use for a matching display type.
        /// </summary>
        public ShapeResult Location(string displayType, string location)
        {
            if(_otherLocations == null)
            {
                _otherLocations = new Dictionary<string, string>(2);
            }

            _otherLocations[displayType] = location;
            return this;
        }

        /// <summary>
        /// Sets a discriminator that is used to find the location of the shape.
        /// </summary>
        public ShapeResult Differentiator(string differentiator)
        {
            _differentiator = differentiator;
            return this;
        }

        /// <summary>
        /// Sets the group identifier the shape will be rendered in.
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public ShapeResult OnGroup(string groupId)
        {
            _groupId = groupId;
            return this;
        }

        /// <summary>
        /// Sets the caching properties of the shape to render.
        /// </summary>
        public ShapeResult Cache(string cacheId, Action<CacheContext> cache = null)
        {
            _cacheId = cacheId;
            _cache = cache;
            return this;
        }
    }
}
