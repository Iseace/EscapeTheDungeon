using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates corner pillars as separate GameObjects.
/// </summary>
public class CornerPillarGenerator
{
    private int height;
    private float size;
    private Material pillarMaterial;
    private Vector3 centerOffset;

    public CornerPillarGenerator(int height, float size, Material material, Vector3 centerOffsetValue = default)
    {
        this.height = height;
        this.size = size;
        pillarMaterial = material;
        centerOffset = centerOffsetValue;
    }

    public void GeneratePillars(
        HashSet<Vector3Int> corners,
        Transform parent)
    {
        GameObject root = new GameObject("CornerPillars");
        root.transform.parent = parent;

        foreach (var c in corners)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"Pillar_{c.x}_{c.z}";
            pillar.transform.parent = root.transform;

            pillar.transform.position =
                new Vector3(c.x, height / 2f, c.z) + centerOffset;

            pillar.transform.localScale =
                new Vector3(size, height, size);

            pillar.GetComponent<MeshRenderer>().material = pillarMaterial;
        }
    }
}
