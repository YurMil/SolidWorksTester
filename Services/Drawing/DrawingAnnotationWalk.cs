using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Safe COM walks for drawing annotations.
    /// Strongly typed interop returns (<c>GetFirstAnnotation3</c>, <c>GetNext3</c>, <c>GetDimension2</c>,
    /// <c>InsertCenterLine2</c>) can throw <see cref="InvalidCastException"/> when the running
    /// SOLIDWORKS build is newer than the compiled interop (e.g. SW 2025 + NuGet 32.1).
    /// Prefer object-returning APIs + <c>as</c>, and catch cast failures.
    /// </summary>
    internal static class DrawingAnnotationWalk
    {
        public static Annotation? GetFirst(IView view)
        {
            try
            {
                return view.GetFirstAnnotation2() as Annotation
                       ?? SafeAnnotation(() => view.GetFirstAnnotation3());
            }
            catch (InvalidCastException)
            {
                return view.GetFirstAnnotation() as Annotation;
            }
            catch
            {
                return null;
            }
        }

        public static Annotation? GetNext(Annotation annotation)
        {
            try
            {
                return annotation.GetNext2() as Annotation
                       ?? SafeAnnotation(() => annotation.GetNext3());
            }
            catch (InvalidCastException)
            {
                return annotation.GetNext() as Annotation;
            }
            catch
            {
                return null;
            }
        }

        public static DisplayDimension? AsDisplayDimension(Annotation annotation)
        {
            if (annotation.GetType() != (int)swAnnotationType_e.swDisplayDimension)
                return null;

            try
            {
                return annotation.GetSpecificAnnotation() as DisplayDimension;
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static Dimension? GetModelDimension(DisplayDimension displayDim, int index = 0)
        {
            try
            {
                // Object-returning GetDimension avoids RCW InvalidCast on GetDimension2.
                return displayDim.GetDimension() as Dimension
                       ?? SafeDimension(() => displayDim.GetDimension2(index));
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Inserts a centerline; treats InvalidCast on the typed return as "maybe succeeded"
        /// and relies on the caller to compare <see cref="IView.GetCenterLineCount"/>.
        /// </summary>
        public static bool TryInsertCenterLine2(IDrawingDoc drawing)
        {
            try
            {
                return drawing.InsertCenterLine2() != null;
            }
            catch (InvalidCastException)
            {
                // Insertion may have succeeded; return type failed to marshal.
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryInsertCenterMark2(IDrawingDoc drawing, int style, bool documentDefaults)
        {
            try
            {
                return drawing.InsertCenterMark2(style, documentDefaults) != null;
            }
            catch (InvalidCastException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Annotation? SafeAnnotation(Func<Annotation?> getter)
        {
            try
            {
                return getter();
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        private static Dimension? SafeDimension(Func<Dimension?> getter)
        {
            try
            {
                return getter();
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }
    }
}
