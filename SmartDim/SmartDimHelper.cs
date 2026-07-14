using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester
{
    /// <summary>
    /// Facade for drawing-view smart dimensioning: entity discovery, selection,
    /// coordinate transforms, and display-dimension helpers.
    /// <para>
    /// Implementation is split across partial files in <c>SmartDim/</c> by responsibility
    /// (view entities, edge geometry, faces, selection, dimensions, features).
    /// </para>
    /// </summary>
    public partial class SmartDimHelper
    {
        private bool _savedDimInputSetting;

        public ISldWorks SwApp { get; }

        public IModelDoc2 Model { get; }

        public IDrawingDoc Drawing { get; }

        public IModelDocExtension Ext { get; }

        /// <summary>
        /// Keys of dimensions already placed in the current drawing session (cross-module dedupe).
        /// </summary>
        public HashSet<string> DimensionedFeatures { get; } =
            new(System.StringComparer.OrdinalIgnoreCase);

        public SmartDimHelper(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IModelDocExtension ext)
        {
            SwApp = swApp;
            Model = model;
            Drawing = drawing;
            Ext = ext;
        }

        // ── UI preferences ───────────────────────────────────────────────

        /// <summary>Suppress the dimension value input dialog to prevent UI popups.</summary>
        public void SuppressDimInput()
        {
            _savedDimInputSetting = SwApp.GetUserPreferenceToggle(
                (int)swUserPreferenceToggle_e.swInputDimValOnCreate);
            SwApp.SetUserPreferenceToggle(
                (int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);
        }

        /// <summary>Restore the dimension input dialog to its previous state.</summary>
        public void RestoreDimInput()
        {
            SwApp.SetUserPreferenceToggle(
                (int)swUserPreferenceToggle_e.swInputDimValOnCreate, _savedDimInputSetting);
        }

        /// <summary>Clears the active drawing selection.</summary>
        public void ClearSelection() => Model.ClearSelection2(true);

        // ── Shared internals (partial files) ─────────────────────────────

        private void ActivateView(IView view) => Drawing.ActivateView(view.GetName2());

        private SelectData CreateViewSelectData(IView view)
        {
            SelectionMgr selMgr = (SelectionMgr)Model.SelectionManager;
            SelectData selData = (SelectData)selMgr.CreateSelectData();
            selData.View = (SolidWorks.Interop.sldworks.View)view;
            return selData;
        }

        private bool TrySelectEntity(Entity entity, IView view, bool append)
        {
            ActivateView(view);
            if (view.SelectEntity(entity, append))
                return true;

            return entity.Select4(append, CreateViewSelectData(view));
        }
    }
}
