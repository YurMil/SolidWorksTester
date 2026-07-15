namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Recognized geometric form of an imported dumb solid.</summary>
    public enum ImportedGeometryShapeKind
    {
        Unknown,
        /// <summary>Extrusion / channel / rail — one axis much longer than cross-section.</summary>
        ElongatedThinProfile,
        /// <summary>Disc or rectangular plate — two large dims and one thin gauge.</summary>
        FlatPlateLike,
        /// <summary>True tube / rod — dominant cylindrical surfaces, elongated bbox.</summary>
        CylindricalLike,
        /// <summary>Bent bracket, housing, multi-body import — moderate bbox, mixed faces.</summary>
        ComplexBracket,
        /// <summary>Similar bounding-box proportions on all axes.</summary>
        BlockyPrismatic
    }
}
