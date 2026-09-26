using System.Collections.Immutable;
using Clipper2Lib;

namespace Quentra.Core;

public sealed class PlanarGeometry
{
    public const int Precision = 6; // Metres, rounded to a micrometre by ClipperD.
    public Point3 Origin { get; }
    public Point3 U { get; }
    public Point3 V { get; }
    public Point3 Normal { get; }
    public PathsD Gross { get; }
    public PathsD Remaining { get; }
    public double GrossArea => Math.Abs(Clipper.Area(Gross));
    public double RemainingArea => Math.Abs(Clipper.Area(Remaining));

    public PlanarGeometry(AreaSnapshot source, LengthUnit unit, TakeoffPolicy policy)
    {
        var factor = Units.MetresPerUnit(unit);
        var points = source.Boundary.Select(x => x.Scale(factor)).ToArray();
        if (points.Length < 3 || points.Any(x => !x.IsFinite))
            throw new ArgumentException("A finite polygon with at least three vertices is required.");
        Origin = points[0];
        // The local x axis follows the first nondegenerate edge, so polygons aligned with
        // global axes keep exact local coordinates through micrometre clipping.
        var edge = points.Skip(1).Select(x => Sub(x, Origin)).FirstOrDefault(x => Norm(x) > policy.LinearToleranceM);
        if (Norm(edge) <= policy.LinearToleranceM) throw new ArgumentException("Degenerate boundary.");
        U = edge.Scale(1 / Norm(edge));
        var cross = points.Select(x => Cross(U, Sub(x, Origin))).MaxBy(Norm);
        if (Norm(cross) <= policy.LinearToleranceM) throw new ArgumentException("Collinear boundary.");
        Normal = cross.Scale(1 / Norm(cross));
        V = Cross(Normal, U);
        Gross = [ProjectRing(source.Boundary, factor, policy)];
        var holes = new PathsD();
        foreach (var ring in source.Openings) holes.Add(ProjectRing(ring, factor, policy));
        var union = holes.Count == 0 ? holes : Clipper.BooleanOp(ClipType.Union, holes, null, FillRule.NonZero, Precision);
        Remaining = holes.Count == 0 ? Gross : Clipper.Difference(Gross, union, FillRule.NonZero, Precision);
        if (!double.IsFinite(GrossArea) || GrossArea <= policy.LinearToleranceM * policy.LinearToleranceM ||
            !double.IsFinite(RemainingArea) || RemainingArea > GrossArea + 1e-8 * Math.Max(1, GrossArea))
            throw new ArgumentException("Invalid polygon area after clipping.");
    }

    public PathD ProjectRing(ImmutableArray<Point3> ring, double factor, TakeoffPolicy policy)
    {
        if (ring.IsDefault || ring.Length < 3 || ring.Length > 1000)
            throw new ArgumentException("Polygon rings require 3–1000 vertices.");
        var result = new PathD();
        foreach (var source in ring)
        {
            var p = Sub(source.Scale(factor), Origin);
            if (!p.IsFinite || Math.Abs(Dot(p, Normal)) > policy.PlanarityToleranceM)
                throw new ArgumentException("Boundary/opening is nonfinite, warped or outside the host plane.");
            var x = Dot(p, U); var y = Dot(p, V);
            if (Math.Abs(x) > 1e7 || Math.Abs(y) > 1e7)
                throw new ArgumentException("Local polygon extent exceeds the supported range.");
            result.Add(new PointD(x, y));
        }
        if (Distance(result[0], result[^1]) <= policy.LinearToleranceM) result.RemoveAt(result.Count - 1);
        if (result.Count < 3) throw new ArgumentException("Degenerate ring.");
        for (var i = 0; i < result.Count; i++)
        {
            if (Distance(result[i], result[(i + 1) % result.Count]) <= policy.LinearToleranceM)
                throw new ArgumentException("Repeated or too-close polygon vertices.");
            for (var j = i + 1; j < result.Count; j++)
            {
                if (j == i + 1 || (i == 0 && j == result.Count - 1)) continue;
                if (Intersects(result[i], result[(i + 1) % result.Count], result[j], result[(j + 1) % result.Count], policy.LinearToleranceM))
                    throw new ArgumentException("Self-intersecting polygon.");
            }
        }
        if (Math.Abs(Clipper.Area(result)) <= policy.LinearToleranceM * policy.LinearToleranceM)
            throw new ArgumentException("Zero-area ring.");
        if (Clipper.Area(result) < 0) result.Reverse();
        return result;
    }

    public (double Gross, double Remaining) AreaInBand(double lower, double upper)
    {
        // The clipping mask is convex even when the subject contains concavity/holes.
        var vertices = Gross.SelectMany(x => x).ToArray();
        var minX = vertices.Min(x => x.x) - 1; var maxX = vertices.Max(x => x.x) + 1;
        var minY = vertices.Min(x => x.y) - 1; var maxY = vertices.Max(x => x.y) + 1;
        var mask = new PathD { new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY) };
        mask = HalfPlane(mask, p => Origin.Z + U.Z * p.x + V.Z * p.y - lower);
        mask = HalfPlane(mask, p => upper - Origin.Z - U.Z * p.x - V.Z * p.y);
        if (mask.Count < 3) return (0, 0);
        return (Math.Abs(Clipper.Area(Clipper.Intersect(Gross, [mask], FillRule.NonZero, Precision))),
            Math.Abs(Clipper.Area(Clipper.Intersect(Remaining, [mask], FillRule.NonZero, Precision))));
    }

    private static PathD HalfPlane(PathD points, Func<PointD, double> distance)
    {
        var result = new PathD();
        if (points.Count == 0) return result;
        var previous = points[^1]; var pd = distance(previous);
        foreach (var point in points)
        {
            var d = distance(point);
            if ((d >= 0) != (pd >= 0))
            {
                var t = pd / (pd - d);
                result.Add(new PointD(previous.x + t * (point.x - previous.x), previous.y + t * (point.y - previous.y)));
            }
            if (d >= 0) result.Add(point);
            previous = point; pd = d;
        }
        return result;
    }

    private static double Distance(PointD a, PointD b) => Math.Sqrt(Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2));
    private static bool Intersects(PointD a, PointD b, PointD c, PointD d, double tolerance)
    {
        static double Turn(PointD p, PointD q, PointD r) => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
        static bool On(PointD p, PointD q, PointD r, double t) => r.x >= Math.Min(p.x, q.x) - t && r.x <= Math.Max(p.x, q.x) + t && r.y >= Math.Min(p.y, q.y) - t && r.y <= Math.Max(p.y, q.y) + t;
        var abC = Turn(a, b, c); var abD = Turn(a, b, d); var cdA = Turn(c, d, a); var cdB = Turn(c, d, b);
        var epsilon = tolerance * Math.Max(Distance(a, b), Distance(c, d));
        if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon)) &&
            ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon))) return true;
        return (Math.Abs(abC) <= epsilon && On(a, b, c, tolerance)) ||
            (Math.Abs(abD) <= epsilon && On(a, b, d, tolerance)) ||
            (Math.Abs(cdA) <= epsilon && On(c, d, a, tolerance)) ||
            (Math.Abs(cdB) <= epsilon && On(c, d, b, tolerance));
    }

    public static Point3 Sub(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static double Dot(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static double Norm(Point3 a) => Math.Sqrt(Dot(a, a));
    public static Point3 Cross(Point3 a, Point3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
