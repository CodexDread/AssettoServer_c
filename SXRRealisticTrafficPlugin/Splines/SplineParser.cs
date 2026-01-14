using System.IO.Compression;
using System.Numerics;

namespace SXRRealisticTrafficPlugin.Splines;

/// <summary>
/// Parser for Assetto Corsa AI spline files (.ai and .aip formats).
/// Handles both single spline files and packed multi-spline archives.
/// </summary>
public class SplineParser
{
    /// <summary>
    /// Load splines from a path (can be .ai single file or .aip/.ai packed archive)
    /// </summary>
    public static SplineData LoadSplines(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Spline file not found: {path}");
        
        // Check if it's a ZIP archive
        using var fs = File.OpenRead(path);
        byte[] header = new byte[4];
        fs.Read(header, 0, 4);
        fs.Position = 0;
        
        bool isZip = header[0] == 0x50 && header[1] == 0x4B; // PK signature
        
        if (isZip)
        {
            return LoadPackedSplines(path);
        }
        else
        {
            var spline = ParseSingleSpline(fs, Path.GetFileName(path));
            return new SplineData
            {
                Splines = new Dictionary<string, Spline> { { spline.Name, spline } }
            };
        }
    }
    
    /// <summary>
    /// Load packed spline archive (.aip or zipped .ai)
    /// </summary>
    private static SplineData LoadPackedSplines(string path)
    {
        var data = new SplineData();
        
        using var archive = ZipFile.OpenRead(path);
        
        // Look for config.yml first
        var configEntry = archive.GetEntry("config.yml");
        if (configEntry != null)
        {
            using var configStream = configEntry.Open();
            using var reader = new StreamReader(configStream);
            data.ConfigYaml = reader.ReadToEnd();
        }
        
        // Parse all .ai files
        foreach (var entry in archive.Entries)
        {
            if (entry.Name.EndsWith(".ai", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                
                var spline = ParseSingleSpline(ms, entry.Name);
                data.Splines[spline.Name] = spline;
            }
        }
        
        return data;
    }
    
    /// <summary>
    /// Parse a single AI spline from a stream
    /// </summary>
    private static Spline ParseSingleSpline(Stream stream, string name)
    {
        using var reader = new BinaryReader(stream);
        
        // Header: 8 bytes
        int version = reader.ReadInt32();     // Usually -1
        int pointCount = reader.ReadInt32();
        
        var spline = new Spline
        {
            Name = name,
            Version = version,
            Points = new SplinePoint[pointCount]
        };
        
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        
        // Read points (20 bytes each: x, y, z, length, unknown)
        for (int i = 0; i < pointCount; i++)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float length = reader.ReadSingle();
            float unknown = reader.ReadSingle();
            
            spline.Points[i] = new SplinePoint
            {
                Position = new Vector3(x, y, z),
                CumulativeLength = length,
                Index = i
            };
            
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
            minZ = Math.Min(minZ, z);
            maxZ = Math.Max(maxZ, z);
        }
        
        spline.Bounds = new SplineBounds
        {
            Min = new Vector3(minX, minY, minZ),
            Max = new Vector3(maxX, maxY, maxZ)
        };
        
        // Calculate total length
        if (pointCount > 0)
        {
            spline.TotalLength = Math.Abs(spline.Points[^1].CumulativeLength);
        }
        
        return spline;
    }
    
    /// <summary>
    /// Get interpolated position along a spline
    /// </summary>
    public static Vector3 GetPositionAtDistance(Spline spline, float distance)
    {
        if (spline.Points.Length == 0)
            return Vector3.Zero;
        
        if (spline.Points.Length == 1)
            return spline.Points[0].Position;
        
        // Handle wraparound
        distance = distance % spline.TotalLength;
        if (distance < 0) distance += spline.TotalLength;
        
        // Binary search for the segment
        int lo = 0, hi = spline.Points.Length - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (Math.Abs(spline.Points[mid].CumulativeLength) <= distance)
                lo = mid;
            else
                hi = mid;
        }
        
        var p1 = spline.Points[lo];
        var p2 = spline.Points[hi];
        
        float segmentLength = Math.Abs(p2.CumulativeLength - p1.CumulativeLength);
        if (segmentLength < 0.001f)
            return p1.Position;
        
        float t = (distance - Math.Abs(p1.CumulativeLength)) / segmentLength;
        t = Math.Clamp(t, 0f, 1f);
        
        return Vector3.Lerp(p1.Position, p2.Position, t);
    }
    
    /// <summary>
    /// Get the direction (forward vector) at a distance along the spline
    /// </summary>
    public static Vector3 GetDirectionAtDistance(Spline spline, float distance)
    {
        var pos1 = GetPositionAtDistance(spline, distance);
        var pos2 = GetPositionAtDistance(spline, distance + 1f);
        
        var direction = pos2 - pos1;
        return direction.Length() > 0.001f ? Vector3.Normalize(direction) : Vector3.UnitZ;
    }
    
    /// <summary>
    /// Find the closest point on a spline to a world position
    /// </summary>
    public static (float distance, float offset) GetClosestPoint(Spline spline, Vector3 worldPos)
    {
        float closestDist = 0;
        float minDistance = float.MaxValue;
        
        for (int i = 0; i < spline.Points.Length; i++)
        {
            float dist = Vector3.Distance(worldPos, spline.Points[i].Position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestDist = Math.Abs(spline.Points[i].CumulativeLength);
            }
        }
        
        return (closestDist, minDistance);
    }
}

/// <summary>
/// Container for loaded spline data
/// </summary>
public class SplineData
{
    public Dictionary<string, Spline> Splines { get; set; } = new();
    public string? ConfigYaml { get; set; }
    
    /// <summary>
    /// Get all main route splines (excluding junctions and exits)
    /// </summary>
    public IEnumerable<Spline> GetMainSplines()
    {
        return Splines.Values.Where(s => 
            s.Name.StartsWith("fast_lane") && 
            !s.Name.Contains("lj") && 
            !s.Name.Contains("lc") &&
            !s.Name.StartsWith("fast_lanea") &&
            !s.Name.StartsWith("fast_laneb") &&
            !s.Name.StartsWith("fast_lanee"));
    }
    
    /// <summary>
    /// Get junction splines
    /// </summary>
    public IEnumerable<Spline> GetJunctionSplines()
    {
        return Splines.Values.Where(s => s.Name.Contains("lj"));
    }
    
    /// <summary>
    /// Get exit splines
    /// </summary>
    public IEnumerable<Spline> GetExitSplines()
    {
        return Splines.Values.Where(s => 
            s.Name.StartsWith("fast_lanea") ||
            s.Name.StartsWith("fast_laneb") ||
            s.Name.StartsWith("fast_lanee"));
    }
}

/// <summary>
/// Single spline track data
/// </summary>
public class Spline
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public SplinePoint[] Points { get; set; } = Array.Empty<SplinePoint>();
    public SplineBounds Bounds { get; set; } = new();
    public float TotalLength { get; set; }
    
    /// <summary>
    /// Get interpolated position at a normalized position (0-1)
    /// </summary>
    public Vector3 GetPositionNormalized(float t)
    {
        return SplineParser.GetPositionAtDistance(this, t * TotalLength);
    }
}

/// <summary>
/// Single point on a spline
/// </summary>
public struct SplinePoint
{
    public Vector3 Position;
    public float CumulativeLength;
    public int Index;
}

/// <summary>
/// Axis-aligned bounding box for a spline
/// </summary>
public class SplineBounds
{
    public Vector3 Min { get; set; }
    public Vector3 Max { get; set; }
    
    public Vector3 Center => (Min + Max) / 2f;
    public Vector3 Size => Max - Min;
    
    public bool Contains(Vector3 point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }
    
    public bool ContainsXZ(float x, float z)
    {
        return x >= Min.X && x <= Max.X && z >= Min.Z && z <= Max.Z;
    }
}
