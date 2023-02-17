using LLGraphicsUnity;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class WanderController : MonoBehaviour {

    public Links links = new Links();
    public Tuner tuner = new Tuner();

    protected Random rand;
    protected GLMaterial gl;
    protected List<Quad> fields = new List<Quad>();
    protected List<CharacterInfo> chars = new List<CharacterInfo>();

    #region unity
    protected void OnEnable() {
        rand = Random.CreateFromIndex((uint)GetInstanceID());
        gl = new GLMaterial();

        fields.Add(links.field.From());

        var n = 1;
        for (var i = 0; i < n; i++) {
            var ch = Instantiate(links.fab);
            ch.gameObject.hideFlags = HideFlags.DontSave;

            var posLocal = rand.NextFloat2(-0.5f, 0.5f);
            var posWorld = links.field.TransformPoint(new float3(posLocal, 0f));
            var rotLocal = quaternion.Euler(0, 0f, rand.NextFloat(0f, 2f * math.PI));

            ch.SetParent(transform, true);
            ch.position = posWorld;
            ch.localRotation = rotLocal;

            chars.Add(new CharacterInfo() {
                tr = ch,
                wanderTarget = new float2(1f, 0f),
            });
        }
    }
    protected void OnDisable() {
        System.Action<Object> destructor = (Application.isPlaying ? Object.Destroy : Object.DestroyImmediate);
        foreach (var ch in chars) {
            destructor(ch.tr);
        }
        chars.Clear();

        if (gl != null) {
            gl.Dispose();
            gl = null;
        }
        fields.Clear();
    }
    protected void Update() {
        var dt = Time.deltaTime;
        foreach (var ch in chars) {
            var pos_world = ((float3)ch.tr.position).xy;
            var forward_world = ((float3)ch.tr.right).xy;
            float2 force_total = default;

            if (fields.IsOutsideQuad(pos_world, out var dist, out var field_dir)) {
                Debug.Log($"Outside");
                force_total += field_dir * tuner.boundary_power;
            } else {
                var wander_force = GetWanderForce(ch);
                force_total += wander_force;
            }

            var velocity = forward_world * tuner.speed;
            velocity += dt * force_total;
            var dir_right3 = math.normalizesafe(new float3(velocity, 0f), TR_X);
            var dir_forward3 = math.normalize(math.cross(TR_Z, dir_right3));
            var rot = quaternion.LookRotationSafe(TR_Z, dir_forward3);
            ch.tr.localRotation = rot;
            pos_world += velocity * dt;
            ch.tr.position = new float3(pos_world, 0f);
        }
    }
    private void OnRenderObject() {
        var c = Camera.current;
        if (!isActiveAndEnabled || (c.cullingMask & (1 << gameObject.layer)) == 0) return;

        var prop = new GLProperty() {
            Color = Color.red,
            ZTestMode = GLProperty.ZTestEnum.ALWAYS,
            ZWriteMode = false,
        };
        using (new GLMatrixScope()) {
            GL.modelview = c.worldToCameraMatrix;
            GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(c.projectionMatrix, false));

            foreach (var ch in chars) {
                var model = ch.tr.localToWorldMatrix;
                using (gl.GetScope(prop))
                using (new GLModelViewScope(model)) {
                    GL.Begin(GL.LINES);
                    GL.Vertex(Vector3.zero);
                    GL.Vertex(new float3(ch.wanderTarget, 0f));
                    GL.End();
                }
            }
        }
    }
    #endregion

    #region methods
    public float2 GetWanderForce(CharacterInfo ch) {
        var wt = ch.wanderTarget;
        wt += rand.NextFloat2(-tuner.wander_jitter, tuner.wander_jitter);
        wt = math.normalizesafe(wt);
        wt *= tuner.wander_radius;
        ch.wanderTarget = wt;

        var pos_world = ch.tr.position;
        var forward_world = ch.tr.right;
        var target_local = new float2(tuner.wander_distance, 0f) + wt;
        var target_world = ch.tr.TransformPoint(new float3(target_local, 0f));
        float3 wander_force = target_world - pos_world;
        return wander_force.xy;
    }
    #endregion

    #region declarations
    public static readonly float3 TR_Z = new float3(0f, 0f, 1f);
    public static readonly float3 TR_X = new float3(1f, 0f, 0f);
    [System.Serializable]
    public class CharacterInfo {
        public Transform tr;
        public float2 wanderTarget;
    }
    [System.Serializable]
    public class Links {
        public Transform fab;
        public Transform field;
    }

    [System.Serializable]
    public class Tuner {
        public float speed;

        public float boundary_power;

        public float wander_distance;
        public float wander_radius;
        public float wander_jitter;
    }
    #endregion
}