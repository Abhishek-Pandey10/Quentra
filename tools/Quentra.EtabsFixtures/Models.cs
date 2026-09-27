#if ETABS_API
using ETABSv1;

namespace Quentra.EtabsFixtures;

// The controlled ETABS models. Each is built in kN-m in a fresh model; the expected quantities are hand-calculated
// in fixtures/etabs/v22.7/golden.json from the dimensions below, independently of the extraction code.
internal static class Models
{
    public sealed record Spec(string Name, string Description, int Stories, double BottomHeight, double TypicalHeight, Action<cSapModel> Build, bool Design = false);

    private static readonly (double X, double Y)[] Corners = [(0, 0), (6, 0), (6, 6), (0, 6)];

    public static readonly Spec[] All =
    [
        new("01-single-beam", "Rectangular beam 300x600, 6.000 m, at the top of a 3.0 m story", 1, 3.0, 3.0, m =>
        {
            Frame(m, 0, 0, 3, 6, 0, 3, "B300x600");
        }),
        new("02-single-column", "Rectangular column 400x600, 3.5 m story", 1, 3.5, 3.5, m =>
        {
            Frame(m, 0, 0, 0, 0, 0, 3.5, "C400x600");
        }),
        new("03-circular-column", "Circular column D500, 3.5 m story", 1, 3.5, 3.5, m =>
        {
            Frame(m, 0, 0, 0, 0, 0, 3.5, "C500D");
        }),
        new("04-slab", "Slab 6 x 8 m, 200 mm, at 3.0 m", 1, 3.0, 3.0, m =>
        {
            Area(m, "S200", (0, 0, 3), (6, 0, 3), (6, 8, 3), (0, 8, 3));
        }),
        new("05-slab-openings", "Slab 6 x 8 m, 200 mm, with two overlapping openings and one crossing the edge", 1, 3.0, 3.0, m =>
        {
            Area(m, "S200", (0, 0, 3), (6, 0, 3), (6, 8, 3), (0, 8, 3));
            Opening(m, (1, 1, 3), (3, 1, 3), (3, 2.5, 3), (1, 2.5, 3));
            Opening(m, (2.5, 2, 3), (4, 2, 3), (4, 3, 3), (2.5, 3, 3));
            Opening(m, (5, 6, 3), (7, 6, 3), (7, 7, 3), (5, 7, 3));
        }),
        new("06-wall", "Wall 5.0 m long, 3.5 m high, 250 mm", 1, 3.5, 3.5, m =>
        {
            Area(m, "W250", (0, 0, 0), (5, 0, 0), (5, 0, 3.5), (0, 0, 3.5));
        }),
        new("07-wall-opening", "Wall 6.0 m x 3.5 m, 250 mm, with a 1.0 x 2.1 m door", 1, 3.5, 3.5, m =>
        {
            Area(m, "W250", (0, 0, 0), (6, 0, 0), (6, 0, 3.5), (0, 0, 3.5));
            Opening(m, (2, 0, 0), (3, 0, 0), (3, 0, 2.1), (2, 0, 2.1));
        }),
        new("08-one-story-frame", "One 6 x 6 m bay, 3.5 m: 4 columns 400x400, 4 beams 300x500, 200 mm slab, 200 mm wall on y = 6", 1, 3.5, 3.5, Bay(3.5)),
        new("09-multi-story", "Three stories (4.0, 3.2, 3.2 m) of the 6 x 6 m bay: columns, beams and slab per story", 3, 4.0, 3.2, m =>
        {
            foreach (var (bottom, top) in new[] { (0.0, 4.0), (4.0, 7.2), (7.2, 10.4) })
            {
                foreach (var (x, y) in Corners) Frame(m, x, y, bottom, x, y, top, "C400x400");
                Beams(m, top);
                Area(m, "S200", (0, 0, top), (6, 0, top), (6, 6, top), (0, 6, top));
            }
        }),
        new("10-meshed", "Slab with openings, wall with door, and beams divided into two objects and slab panels; analysis mesh created", 1, 3.5, 3.5, m =>
        {
            Area(m, "S200", (0, 0, 3.5), (6, 0, 3.5), (6, 8, 3.5), (0, 8, 3.5));
            Opening(m, (1, 1, 3.5), (3, 1, 3.5), (3, 2.5, 3.5), (1, 2.5, 3.5));
            Area(m, "W250", (0, 0, 0), (6, 0, 0), (6, 0, 3.5), (0, 0, 3.5));
            Opening(m, (2, 0, 0), (3, 0, 0), (3, 0, 2.1), (2, 0, 2.1));
            // The same 6 m beam as two objects, and a second 6 x 8 slab as four panels: user divisions are real objects.
            Frame(m, 0, 12, 3.5, 3, 12, 3.5, "B300x600");
            Frame(m, 3, 12, 3.5, 6, 12, 3.5, "B300x600");
            Frame(m, 1.5, 10, 3.5, 1.5, 14, 3.5, "B300x600");   // crosses the first half, so the analysis mesh splits both

            foreach (var (x, y) in new[] { (10.0, 0.0), (13.0, 0.0), (10.0, 4.0), (13.0, 4.0) })
                Area(m, "S200", (x, y, 3.5), (x + 3, y, 3.5), (x + 3, y + 4, 3.5), (x, y + 4, 3.5));
        }),
        new("12-unsupported", "Unsupported cases next to one supported beam", 1, 3.5, 3.5, m =>
        {
            Frame(m, 0, 0, 3.5, 6, 0, 3.5, "B300x600");       // supported: 1.080 m³
            Frame(m, 0, 3, 3.5, 6, 3, 3.5, "NP-BEAM");        // non-prismatic
            Frame(m, 0, 6, 3.5, 6, 6, 3.5, "T-BEAM");         // concrete tee
            Frame(m, 0, 9, 3.5, 6, 9, 3.5, "STEEL-I");        // steel: out of scope
            Frame(m, 0, 12, 3.5, 6, 12, 3.5, "C500D");        // circular section used as a beam
            Frame(m, 0, 15, 0, 6, 15, 3.5, "B300x600");       // inclined: ETABS orients it as a brace
            Area(m, "RIBBED", (10, 0, 3.5), (16, 0, 3.5), (16, 6, 3.5), (10, 6, 3.5));
            Area(m, "DECK", (10, 8, 3.5), (16, 8, 3.5), (16, 14, 3.5), (10, 14, 3.5));
            Area(m, "S200", (20, 0, 3.5), (26, 0, 3.5), (26, 6, 3.55), (20, 6, 3.5));   // warped by 50 mm
            Area(m, "None", (20, 8, 3.5), (26, 8, 3.5), (26, 14, 3.5), (20, 14, 3.5));  // null area
            Opening(m, (40, 0, 3.5), (41, 0, 3.5), (41, 1, 3.5), (40, 1, 3.5));        // opening over nothing
        }),
        new("13-designed-frame", "The one-story bay (without the wall) analysed and designed; one column section with modeled bars to be checked", 1, 3.5, 3.5, m =>
        {
            foreach (var (x, y) in Corners) Frame(m, x, y, 0, x, y, 3.5, (x, y) == (6, 6) ? "C400x400-CHECK" : "C400x400");
            Beams(m, 3.5);
            Area(m, "S200", (0, 0, 3.5), (6, 0, 3.5), (6, 6, 3.5), (0, 6, 3.5));
        }, Design: true),
    ];

    private static Action<cSapModel> Bay(double h) => m =>
    {
        foreach (var (x, y) in Corners) Frame(m, x, y, 0, x, y, h, "C400x400");
        Beams(m, h);
        Area(m, "S200", (0, 0, h), (6, 0, h), (6, 6, h), (0, 6, h));
        Area(m, "W200", (0, 6, 0), (6, 6, 0), (6, 6, h), (0, 6, h));
    };

    private static void Beams(cSapModel m, double z)
    {
        Frame(m, 0, 0, z, 6, 0, z, "B300x500"); Frame(m, 6, 0, z, 6, 6, z, "B300x500");
        Frame(m, 6, 6, z, 0, 6, z, "B300x500"); Frame(m, 0, 6, z, 0, 0, z, "B300x500");
    }

    public static void Properties(cSapModel m)
    {
        Check(m.PropMaterial.SetMaterial("C30", eMatType.Concrete), "concrete material");
        Check(m.PropMaterial.SetOConcrete_1("C30", 30000, false, 1, 2, 4, 0.002, 0.0035, -0.1), "concrete strength");
        Check(m.PropMaterial.SetWeightAndMass("C30", 1, 25), "concrete weight");
        Check(m.PropMaterial.SetMaterial("B500", eMatType.Rebar), "rebar material");
        Check(m.PropMaterial.SetORebar_1("B500", 500000, 550000, 500000, 550000, 1, 1, 0.01, 0.09, -0.1, false), "rebar strength");
        Check(m.PropMaterial.SetMaterial("S355", eMatType.Steel), "steel material");
        Check(m.PropFrame.SetRectangle("B300x600", "C30", 0.6, 0.3), "B300x600");
        Check(m.PropFrame.SetRectangle("B300x500", "C30", 0.5, 0.3), "B300x500");
        Check(m.PropFrame.SetRectangle("B300x400", "C30", 0.4, 0.3), "B300x400");
        Check(m.PropFrame.SetRectangle("C400x600", "C30", 0.6, 0.4), "C400x600");
        Check(m.PropFrame.SetRectangle("C400x400", "C30", 0.4, 0.4), "C400x400");
        Check(m.PropFrame.SetRectangle("C400x400-CHECK", "C30", 0.4, 0.4), "C400x400-CHECK");
        Check(m.PropFrame.SetCircle("C500D", "C30", 0.5), "C500D");
        foreach (var beam in new[] { "B300x600", "B300x500", "B300x400" })
            Check(m.PropFrame.SetRebarBeam(beam, "B500", "B500", 0.05, 0.05, 0, 0, 0, 0), beam + " beam rebar");
        var bar = RebarName(m, 0.02); var tie = RebarName(m, 0.01);
        Check(m.PropFrame.SetRebarColumn("C400x600", "B500", "B500", 1, 1, 0.04, 0, 3, 3, bar, tie, 0.15, 2, 2, true), "C400x600 rebar");
        Check(m.PropFrame.SetRebarColumn("C400x400", "B500", "B500", 1, 1, 0.04, 0, 3, 3, bar, tie, 0.15, 2, 2, true), "C400x400 rebar");
        // Modeled bars to be checked: 3 per face, 8 bars in all.
        Check(m.PropFrame.SetRebarColumn("C400x400-CHECK", "B500", "B500", 1, 1, 0.04, 0, 3, 3, bar, tie, 0.15, 2, 2, false), "C400x400-CHECK rebar");
        Check(m.PropFrame.SetRebarColumn("C500D", "B500", "B500", 2, 2, 0.04, 8, 0, 0, bar, tie, 0.15, 0, 0, true), "C500D rebar");
        string[] start = ["B300x600"], end = ["B300x400"]; double[] length = [1]; int[] type = [1], ei33 = [1], ei22 = [1];
        Check(m.PropFrame.SetNonPrismatic("NP-BEAM", 1, ref start, ref end, ref length, ref type, ref ei33, ref ei22), "non-prismatic");
        Check(m.PropFrame.SetConcreteTee("T-BEAM", "C30", 0.6, 1.0, 0.15, 0.3, 0.3, false), "concrete tee");
        Check(m.PropFrame.SetISection("STEEL-I", "S355", 0.4, 0.2, 0.012, 0.008, 0.2, 0.012), "steel I");
        Check(m.PropArea.SetSlab("S200", eSlabType.Slab, eShellType.ShellThin, "C30", 0.2), "S200");
        Check(m.PropArea.SetWall("W250", eWallPropType.Specified, eShellType.ShellThin, "C30", 0.25), "W250");
        Check(m.PropArea.SetWall("W200", eWallPropType.Specified, eShellType.ShellThin, "C30", 0.2), "W200");
        Check(m.PropArea.SetSlab("RIBBED", eSlabType.Ribbed, eShellType.ShellThin, "C30", 0.1), "ribbed slab");
        Check(m.PropArea.SetSlabRibbed("RIBBED", 0.4, 0.1, 0.15, 0.12, 1.0, 1), "ribbed slab ribs");
        Check(m.PropArea.SetDeck("DECK", eDeckType.Filled, eShellType.ShellThin, "C30", 0.15), "deck");
    }

    // ETABS names bar sizes by the model's unit system; pick the defined bar closest to the wanted diameter.
    private static string RebarName(cSapModel m, double diameter)
    {
        int n = 0; string[]? names = null;
        Check(m.PropRebar.GetNameList(ref n, ref names), "rebar list");
        return names!.Take(n).Select(name => { double a = 0, d = 0; m.PropRebar.GetRebarProps(name, ref a, ref d); return (name, d); })
            .OrderBy(x => Math.Abs(x.d - diameter)).First().name;
    }

    private static void Frame(cSapModel m, double x1, double y1, double z1, double x2, double y2, double z2, string section)
    {
        string name = "";
        Check(m.FrameObj.AddByCoord(x1, y1, z1, x2, y2, z2, ref name, section), $"frame {section}");
    }

    private static string Area(cSapModel m, string property, params (double X, double Y, double Z)[] points)
    {
        double[] x = [.. points.Select(p => p.X)], y = [.. points.Select(p => p.Y)], z = [.. points.Select(p => p.Z)];
        string name = "";
        Check(m.AreaObj.AddByCoord(points.Length, ref x, ref y, ref z, ref name, property), $"area {property}");
        return name;
    }

    private static void Opening(cSapModel m, params (double X, double Y, double Z)[] points)
    {
        var name = Area(m, "None", points);
        Check(m.AreaObj.SetOpening(name, true), "opening");
    }

    public static void Check(int ret, string what)
    {
        if (ret != 0) throw new InvalidOperationException($"ETABS returned {ret} while creating {what}.");
    }
}
#endif
