using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        private readonly Dictionary<string, Edge[]> _viewEdgeCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Face2[]> _viewFaceCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Cached visible edges — one COM call per view per drawing pass.</summary>
        public Edge[] GetViewEdgesCached(IView view)
        {
            string name = view.GetName2();
            if (!_viewEdgeCache.TryGetValue(name, out Edge[]? edges))
            {
                edges = GetViewEdges(view);
                _viewEdgeCache[name] = edges;
            }

            return edges;
        }

        public Face2[] GetViewFacesCached(IView view)
        {
            string name = view.GetName2();
            if (!_viewFaceCache.TryGetValue(name, out Face2[]? faces))
            {
                faces = GetViewFaces(view);
                _viewFaceCache[name] = faces;
            }

            return faces;
        }

        public void PreCacheViewEdges(IView view) => _ = GetViewEdgesCached(view);

        public void ClearViewCaches()
        {
            _viewEdgeCache.Clear();
            _viewFaceCache.Clear();
        }
    }
}
