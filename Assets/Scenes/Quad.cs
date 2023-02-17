using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public struct Quad {
    public float2 center;
    public float2 size;
    public float angle;

    bool model_valid;
    float4x4 model;

    bool model_inv_valid;
    float4x4 model_inverse;

    public float4x4 Model {
        get {
            if (!model_valid) {
                model_valid = true;
                model = float4x4.TRS(
                    new float3(center, 0f),
                    quaternion.EulerXYZ(0f, 0f, math.radians(angle)),
                    new float3(size, 1f)
            );
            }
            return model;
        }
    }
    public float4x4 ModelInverse {
        get {
            if (!model_inv_valid) {
                model_inv_valid = true;
                model_inverse = math.inverse(Model);
            }
            return model_inverse;
        }
    }
    public void Invalidate() {
        model_valid = false;
        model_inv_valid = false;
    }
}

public static class QuadExtension {

    public static Quad From(this Transform quad) {
        return new Quad() {
            center = ((float3)quad.localPosition).xy,
            size = ((float3)quad.localScale).xy,
            angle = quad.localRotation.z,
        };
    }

    public static bool IsOutsideQuad(this Quad quad, float2 pos_world2, out float distance, out float2 field_dir) {
        var model = quad.Model;
        var model_inv = quad.ModelInverse;
        var pos_local = math.mul(model_inv, new float4(pos_world2, 0f, 1f)).xy;

        var comp_x = (pos_local.x < -MAX ? -1 : (pos_local.x > MAX ? 1 : 0));
        var comp_y = (pos_local.y < -MAX ? -1 : (pos_local.y > MAX ? 1 : 0));

        var closest_pos = new float2(
            comp_x == -1 ? -MAX : (comp_x == 0 ? pos_local.x : MAX),
            comp_y == -1 ? -MAX : (comp_y == 0 ? pos_local.y : MAX)
            );

        var closest_world2 = math.mul(model, new float4(closest_pos, 0f, 1f)).xy;
        field_dir = closest_world2 - pos_local;
        field_dir = math.mul(model, new float4(field_dir, 0f, 0f)).xy;

        var sq_dist = math.lengthsq(field_dir);
        if (sq_dist < 1e-3f || (comp_x == 0 && comp_y == 0)) {
            distance = default;
            field_dir = default;
            return false;
        }

        Debug.Log($"pos={pos_local} closest={closest_pos} dir={field_dir}");

        distance = math.sqrt(sq_dist);
        field_dir /= distance;
        return true;
    }
    public static bool IsOutsideQuad(this IEnumerable<Quad> quads, float2 pos_world2, out float distance, out float2 field_dir) {
        distance = float.MaxValue;
        field_dir = default;
        var result = false;
        foreach (var q in quads) {
            if (q.IsOutsideQuad(pos_world2, out var tmp_distance, out var tmp_dir)) {
                result = true;
                if (tmp_distance >= distance) continue;
                distance = tmp_distance;
                field_dir = tmp_dir;
            }
        }
        return result;
    }

    #region declarations
    public const float MAX = 0.5f;
    #endregion
}