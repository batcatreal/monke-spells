using System.Collections.Generic;
using UnityEngine;

namespace GTAGWizard.Visuals
{
    /// <summary>
    /// Accumulates simple primitives into ONE mesh (so the whole staff is only 3 draw calls).
    /// Everything is generated in code: no prefabs, no asset bundles, nothing that can be "missing".
    ///
    /// Winding note: Unity front faces are clockwise as seen from the front, i.e. a triangle (p0,p1,p2)
    /// faces along Vector3.Cross(p1 - p0, p2 - p0). All triangles below follow that rule.
    /// </summary>
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>
        /// Adds a (possibly tapered) cylinder centred on xf's origin, axis along xf's +Y.
        /// rBottom is the radius at -height/2, rTop the radius at +height/2.
        /// </summary>
        public void AddCylinder(Matrix4x4 xf, float rBottom, float rTop, float height, int segments, Color tint,
                                bool capBottom = true, bool capTop = true)
        {
            segments = Mathf.Max(3, segments);
            float half = height * 0.5f;
            float slope = height > 0.0001f ? (rBottom - rTop) / height : 0f;

            // Side wall. The seam vertices are duplicated so normals/UVs stay clean.
            int wall = _vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = u * Mathf.PI * 2f;
                float cx = Mathf.Cos(angle);
                float sz = Mathf.Sin(angle);
                Vector3 normal = xf.MultiplyVector(new Vector3(cx, slope, sz)).normalized;

                AddVertex(xf.MultiplyPoint3x4(new Vector3(cx * rBottom, -half, sz * rBottom)), normal, tint, new Vector2(u, 0f));
                AddVertex(xf.MultiplyPoint3x4(new Vector3(cx * rTop, half, sz * rTop)), normal, tint, new Vector2(u, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = wall + i * 2;
                int t0 = b0 + 1;
                int b1 = b0 + 2;
                int t1 = b0 + 3;
                AddTriangle(b0, t0, b1);
                AddTriangle(t0, t1, b1);
            }

            if (capTop && rTop > 0.0001f) AddCap(xf, rTop, half, true, segments, tint);
            if (capBottom && rBottom > 0.0001f) AddCap(xf, rBottom, -half, false, segments, tint);
        }

        /// <summary>Adds a tapered cylinder running from point a (radius rA) to point b (radius rB).</summary>
        public void AddSegment(Vector3 a, Vector3 b, float rA, float rB, int segments, Color tint)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.00001f) return;

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            Matrix4x4 xf = Matrix4x4.TRS((a + b) * 0.5f, rotation, Vector3.one);
            AddCylinder(xf, rA, rB, length, segments, tint);
        }

        /// <summary>
        /// Adds a faceted, crystal-like gem: pointed top, short straight "belt", pointed bottom.
        /// Each facet has its own (flat) normal and a slightly different vertex-colour brightness.
        /// </summary>
        public void AddGem(Matrix4x4 xf, float radius, float height, int sides, Color tint)
        {
            sides = Mathf.Max(3, sides);
            float top = height * 0.5f;
            float belt = height * 0.14f;
            Vector3 apexTop = new Vector3(0f, top, 0f);
            Vector3 apexBottom = new Vector3(0f, -top, 0f);

            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                Vector3 u0 = new Vector3(Mathf.Cos(a0) * radius, belt, Mathf.Sin(a0) * radius);
                Vector3 u1 = new Vector3(Mathf.Cos(a1) * radius, belt, Mathf.Sin(a1) * radius);
                Vector3 l0 = new Vector3(u0.x, -belt, u0.z);
                Vector3 l1 = new Vector3(u1.x, -belt, u1.z);

                float variation = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(i * 2.1f + 0.7f));

                AddFlatTriangle(xf, u0, apexTop, u1, Shade(tint, 1.00f * variation));      // upper facet
                AddFlatTriangle(xf, l0, u0, l1, Shade(tint, 0.90f * variation));           // belt, lower triangle
                AddFlatTriangle(xf, u0, u1, l1, Shade(tint, 0.95f * variation));           // belt, upper triangle
                AddFlatTriangle(xf, l0, l1, apexBottom, Shade(tint, 0.70f * variation));   // lower facet
            }
        }

        public Mesh Build(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- internals

        private void AddCap(Matrix4x4 xf, float radius, float y, bool top, int segments, Color tint)
        {
            Vector3 normal = xf.MultiplyVector(top ? Vector3.up : Vector3.down).normalized;

            int centre = _vertices.Count;
            AddVertex(xf.MultiplyPoint3x4(new Vector3(0f, y, 0f)), normal, tint, new Vector2(0.5f, 0.5f));

            int ring = _vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cx = Mathf.Cos(angle);
                float sz = Mathf.Sin(angle);
                AddVertex(xf.MultiplyPoint3x4(new Vector3(cx * radius, y, sz * radius)), normal, tint,
                          new Vector2(0.5f + cx * 0.5f, 0.5f + sz * 0.5f));
            }

            for (int i = 0; i < segments; i++)
            {
                if (top) AddTriangle(centre, ring + i + 1, ring + i);
                else AddTriangle(centre, ring + i, ring + i + 1);
            }
        }

        private void AddFlatTriangle(Matrix4x4 xf, Vector3 p0, Vector3 p1, Vector3 p2, Color tint)
        {
            Vector3 w0 = xf.MultiplyPoint3x4(p0);
            Vector3 w1 = xf.MultiplyPoint3x4(p1);
            Vector3 w2 = xf.MultiplyPoint3x4(p2);
            Vector3 normal = Vector3.Cross(w1 - w0, w2 - w0).normalized;

            int start = _vertices.Count;
            AddVertex(w0, normal, tint, new Vector2(0f, 0f));
            AddVertex(w1, normal, tint, new Vector2(1f, 0f));
            AddVertex(w2, normal, tint, new Vector2(0.5f, 1f));
            AddTriangle(start, start + 1, start + 2);
        }

        private void AddVertex(Vector3 position, Vector3 normal, Color color, Vector2 uv)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _colors.Add(color);
            _uvs.Add(uv);
        }

        private void AddTriangle(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        private static Color Shade(Color c, float brightness)
        {
            return new Color(c.r * brightness, c.g * brightness, c.b * brightness, c.a);
        }
    }
}
