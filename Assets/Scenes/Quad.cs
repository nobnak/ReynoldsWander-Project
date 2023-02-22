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

    bool uv_valid;
    float4 uv;

    public float4x4 Model {
        get {
            ValidateModel();
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
    public float2 U {
        get {
            ValidateModel();
            return uv.xy;
        }
    }
    public float2 V {
        get {
            ValidateModel();
            return uv.zw;
        }
    }
    public void Invalidate() {
        model_valid = false;
        model_inv_valid = false;
        uv_valid = false;
    }

    void ValidateModel() {
        if (!model_valid) {
            model_valid = true;
            var model_t = new float3(center, 0f);
            var model_r = quaternion.EulerXYZ(0f, 0f, math.radians(angle));
            var model_s = new float3(size, 1f);

            model = float4x4.TRS(model_t, model_r, model_s);
            uv = new float4(
                math.mul(model_r, new float3(1f, 0f, 0f)).xy, 
                math.mul(model_r, new float3(0f, 1f, 0f)).xy);
        }
    }

}

public static class QuadExtension {

    public static Quad From(this Transform quad) {
        return new Quad() {
            center = ((float3)quad.localPosition).xy,
            size = ((float3)quad.localScale).xy,
            angle = quad.eulerAngles.z,
        };
    }

    public static float SignedDistance(this Quad quad, float2 point_world2, out float2 closest_world2) {
        var q_u = quad.U;
        var q_v = quad.V;
        var q_center = quad.center;
        var q_extent = quad.size * 0.5f;
        var point_q = point_world2 - q_center;
        var x = math.dot(q_u, point_q);
        var y = math.dot(q_v, point_q);

        var dx = math.abs(x) - q_extent.x;
        var dy = math.abs(y) - q_extent.y;
        var bound_x = math.clamp(x, -q_extent.x, q_extent.x);
        var bound_y = math.clamp(y, -q_extent.y, q_extent.y);
        var fixed_x = math.sign(x) * q_extent.x;
        var fixed_y = math.sign(y) * q_extent.y;
        var closest_on_x = fixed_x * q_u + bound_y * q_v;
        var closest_on_y = bound_x * q_u + fixed_y * q_v;
        if (math.distancesq(point_q, closest_on_x) < math.distancesq(point_q, closest_on_y))
            closest_world2 = q_center + closest_on_x;
        else
            closest_world2 = q_center + closest_on_y;

        var dist_sign = (dx < 0 && dy < 0) ? -1 : 1;
        Debug.Log($"sign={dist_sign}, xy=({x},{y}) uv=({q_u},{q_v})");

        var field_dir = closest_world2 - point_world2;
        var distance = math.length(field_dir);
        return dist_sign * distance;
    }
    public static float SignedDistance(this IEnumerable<Quad> quads, float2 pos_world2, out float2 closest_world2) {
        var distance = float.MaxValue;
        closest_world2 = default;

        var iter = quads.GetEnumerator();
        for (var i = 0; iter.MoveNext(); i++) {
            var q = iter.Current;
            var tmp_distance = q.SignedDistance(pos_world2, out var tmp_closest_world2);
            if (distance > 0f) {
                if (tmp_distance < distance) {
                    distance = tmp_distance;
                    closest_world2 = tmp_closest_world2;
                }
            } else {
                if (distance < tmp_distance && tmp_distance <= 0f) {
                    distance = tmp_distance;
                    closest_world2 = tmp_closest_world2;
                }
            }
        }

        return distance;
    }

    #region declarations
    public const float MAX = 0.5f;
    #endregion
}