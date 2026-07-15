using System;
using System.Collections.Generic;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Result of matching EST configuration property <c>Name</c> to a drawing pipeline.</summary>
    public sealed class EstNameIdentification
    {
        public string RawName { get; init; } = string.Empty;
        public string NormalizedName { get; init; } = string.Empty;
        /// <summary>Stable slug for routing/logging, e.g. <c>pipe</c>, <c>flange_blind_plate</c>.</summary>
        public string CatalogId { get; init; } = string.Empty;
        public PartModelKind PartKind { get; init; }
        public FlatPlateSubKind FlatPlateSubKind { get; init; } = FlatPlateSubKind.Unknown;
        /// <summary>True when SolidWorksTester has a dedicated dimension pipeline for this family.</summary>
        public bool HasDedicatedPipeline { get; init; }
    }

    /// <summary>
    /// EST PDM template <c>Name</c> catalog (PIPE, PLATE, FLANGE, SHELL, …).
    /// Rules are evaluated top-to-bottom; first match wins.
    /// </summary>
    internal static class EstNameRegistry
    {
        private sealed record Rule(
            string CatalogId,
            PartModelKind Kind,
            FlatPlateSubKind SubKind,
            bool DedicatedPipeline,
            Func<string, bool> Match);

        private static readonly Rule[] Rules =
        [
            // ── Exact / highly specific ──
            new Rule("flange_blind_plate", PartModelKind.FlatPlate, FlatPlateSubKind.FlangeGasket, true,
                n => n == "FLANGE (BLIND, PLATE)"),
            new Rule("dished_end_din28011", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "DISHED END DIN28011"),
            new Rule("dished_end_din28013", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "DISHED END DIN28013"),
            new Rule("dished_end_ss895", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "DISHED END SS895"),
            new Rule("plate_round", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, true,
                n => n == "PLATE ROUND"),
            new Rule("shell_with_cutting", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "SHELL WITH CUTTING"),
            new Rule("repad_round_dished_end", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, false,
                n => n == "RE-PAD ROUND DISHED END"),
            new Rule("repad_rectangular", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n == "RE-PAD RECTANGULAR"),
            new Rule("repad_round", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, false,
                n => n == "RE-PAD ROUND"),
            new Rule("bended_plate", PartModelKind.BentSheetMetal, FlatPlateSubKind.Unknown, true,
                n => n == "BENDED PLATE"),
            new Rule("insulation_ring", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, false,
                n => n == "INSULATION RING"),
            new Rule("insulation_shell", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "INSULATION SHELL"),
            new Rule("stiffening_ring_section", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n == "STIFFENING RING SECTION"),
            new Rule("nozzle_support_horizontal", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n.StartsWith("NOZZLE SUPPORT (HORIZONTAL", StringComparison.Ordinal)),
            new Rule("nozzle_support_vertical", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n.StartsWith("NOZZLE SUPPORT (VERTICAL", StringComparison.Ordinal)),
            new Rule("lifting_lug_vertical", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n.StartsWith("LIFTING LUG (VERT", StringComparison.Ordinal)),
            new Rule("lifting_lug_free", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n.StartsWith("LIFTING LUG (FREE", StringComparison.Ordinal)),

            // ── Single-word / short exact ──
            new Rule("pipe", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, true,
                n => n == "PIPE"),
            new Rule("plate", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, true,
                n => n == "PLATE"),
            new Rule("gasket", PartModelKind.FlatPlate, FlatPlateSubKind.FlangeGasket, true,
                n => n == "GASKET"),
            new Rule("shell", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "SHELL"),
            new Rule("cone", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "CONE"),
            new Rule("bellow", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n == "BELLOW"),
            new Rule("grating", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n == "GRATING"),
            new Rule("nameplate", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n == "NAMEPLATE"),

            // ── Structural sections (profile = name; generic/import pipeline) ──
            new Rule("flat_bar", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false, n => n == "FLAT BAR"),
            new Rule("round_bar", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false, n => n == "ROUND BAR"),
            new Rule("square_bar", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "SQUARE BAR"),
            new Rule("angle", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "ANGLE"),
            new Rule("ipe", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "IPE"),
            new Rule("inp", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "INP"),
            new Rule("hea", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "HEA"),
            new Rule("heb", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "HEB"),
            new Rule("unp", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "UNP"),
            new Rule("upe", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "UPE"),
            new Rule("shs", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "SHS"),
            new Rule("rhs", PartModelKind.ImportedGeometry, FlatPlateSubKind.Unknown, false, n => n == "RHS"),

            // ── Contains fallbacks (less specific) ──
            new Rule("elbow", PartModelKind.BentSheetMetal, FlatPlateSubKind.Unknown, false,
                n => n.Contains("ELBOW", StringComparison.Ordinal)),
            new Rule("dished_end", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, false,
                n => n.Contains("DISHED END", StringComparison.Ordinal)),
            new Rule("flange", PartModelKind.FlatPlate, FlatPlateSubKind.FlangeGasket, true,
                n => n.Contains("FLANGE", StringComparison.Ordinal)),
            new Rule("gasket_fuzzy", PartModelKind.FlatPlate, FlatPlateSubKind.FlangeGasket, true,
                n => n.Contains("GASKET", StringComparison.Ordinal)),
            new Rule("repad", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n.Contains("RE-PAD", StringComparison.Ordinal) || n.Contains("REPAD", StringComparison.Ordinal)),
            new Rule("lifting_lug", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n.Contains("LIFTING LUG", StringComparison.Ordinal)),
            new Rule("nozzle_support", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, false,
                n => n.Contains("NOZZLE SUPPORT", StringComparison.Ordinal)),
            new Rule("insulation", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, false,
                n => n.Contains("INSULATION RING", StringComparison.Ordinal)),
            new Rule("tube", PartModelKind.Cylindrical, FlatPlateSubKind.Unknown, true,
                n => n.Contains("PIPE", StringComparison.Ordinal) || n.Contains("TUBE", StringComparison.Ordinal)),
            new Rule("round_plate", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, true,
                n => n.Contains("PLATE ROUND", StringComparison.Ordinal) ||
                     (n.Contains("ROUND", StringComparison.Ordinal) && n.Contains("PLATE", StringComparison.Ordinal))),
            new Rule("plate_fuzzy", PartModelKind.FlatPlate, FlatPlateSubKind.Generic, true,
                n => n.Contains("PLATE", StringComparison.Ordinal)),
            new Rule("ring", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, false,
                n => n.Contains("RING", StringComparison.Ordinal)),
            new Rule("disc", PartModelKind.FlatPlate, FlatPlateSubKind.RoundDisc, false,
                n => n.Contains("DISC", StringComparison.Ordinal) || n.Contains("DISK", StringComparison.Ordinal)),
        ];

        public static bool TryIdentify(string? rawName, out EstNameIdentification identification)
        {
            identification = null!;
            if (string.IsNullOrWhiteSpace(rawName))
                return false;

            string normalized = Normalize(rawName);

            foreach (Rule rule in Rules)
            {
                if (!rule.Match(normalized))
                    continue;

                identification = new EstNameIdentification
                {
                    RawName = rawName.Trim(),
                    NormalizedName = normalized,
                    CatalogId = rule.CatalogId,
                    PartKind = rule.Kind,
                    FlatPlateSubKind = rule.SubKind,
                    HasDedicatedPipeline = rule.DedicatedPipeline
                };
                return true;
            }

            return false;
        }

        public static string Normalize(string rawName)
        {
            string s = rawName.Trim().ToUpperInvariant();
            while (s.Contains("  ", StringComparison.Ordinal))
                s = s.Replace("  ", " ", StringComparison.Ordinal);
            return s;
        }

        /// <summary>All catalog ids for documentation and tests.</summary>
        public static IReadOnlyList<string> ListCatalogIds()
        {
            var ids = new List<string>();
            foreach (Rule rule in Rules)
            {
                if (!ids.Contains(rule.CatalogId, StringComparer.OrdinalIgnoreCase))
                    ids.Add(rule.CatalogId);
            }

            return ids;
        }
    }
}
