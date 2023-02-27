using LLGraphicsUnity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    protected List<DataForDebug> forDebugData = new List<DataForDebug>();

    #region unity
    protected void OnEnable() {
        rand = Random.CreateFromIndex((uint)GetInstanceID());
        gl = new GLMaterial();

        var fields_tr = links.fields.GetComponentsInChildren<Transform>().Where(v => v.parent == links.fields).ToArray();
        fields.AddRange(fields_tr.Select(v => v.From()));

        var n = tuner.n;
        for (var i = 0; i < n; i++) {
            var ch = Instantiate(links.fab);
            ch.gameObject.hideFlags = HideFlags.DontSave;

            var posLocal = rand.NextFloat2(-0.5f, 0.5f);
            var posWorld = fields_tr[0].TransformPoint(new float3(posLocal, 0f));
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
        forDebugData.Clear();

        for (var i = 0; i < chars.Count; i++) { 
            var ch = chars[i];
            var pos_world = ((float3)ch.tr.position).xy;
            var forward_world = ((float3)ch.tr.right).xy;
            var search_world = pos_world + tuner.search_distance * forward_world;
            float2 force_total = default;

            var closest_dist = fields.SignedDistance(search_world, out var closest_pos);
            if (closest_dist > 1e-2f) {
                force_total += math.normalize(closest_pos - search_world)
                    * math.smoothstep(0f, tuner.boundary_width, closest_dist) * tuner.boundary_power;
            }

            var wander_force = GetWanderForce(ch);
            force_total += wander_force;

            var velocity = forward_world * tuner.speed;
            velocity += dt * force_total;
            var dir_right3 = math.normalizesafe(new float3(velocity, 0f), TR_X);
            var dir_forward3 = math.normalize(math.cross(TR_Z, dir_right3));
            var rot = quaternion.LookRotationSafe(TR_Z, dir_forward3);
            ch.tr.localRotation = rot;
            pos_world += velocity * dt;
            ch.tr.position = new float3(pos_world, 0f);

            forDebugData.Add(new DataForDebug() {
                index = i,
                totalForce = force_total,
                boundary_pos = closest_pos,
                center_pos = pos_world,
                search_pos = search_world,
            });
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

            for (var i = 0; i < chars.Count; i++) {
                var ch = chars[i];

                using (new GLModelViewScope(ch.tr.localToWorldMatrix))
                using (gl.GetScope(new GLProperty(prop) { Color = Color.magenta })) {
                    var wander_center = TR_X * tuner.wander_distance;
                    GL.Begin(GL.LINE_STRIP);
                    //GL.Vertex(Vector3.zero);
                    GL.Vertex(wander_center);
                    GL.Vertex(wander_center + new float3(ch.wanderTarget, 0f));
                    GL.End();
                }

                var ifor = forDebugData.FindIndex(v => v.index == i);
                if (ifor >= 0) {
                    var ch_debug = forDebugData[ifor];
                    var center = new float3(ch_debug.center_pos, 0f);
                    var search = new float3(ch_debug.search_pos, 0f);
                    var boundary = new float3(ch_debug.boundary_pos, 0f);

                    using (gl.GetScope(new GLProperty(prop) { Color = Color.red })) {
                        GL.Begin(GL.LINES);
                        GL.Vertex(center);
                        GL.Vertex(center + new float3(ch_debug.totalForce, 0f));
                        GL.End();
                    }
                    using (gl.GetScope(new GLProperty(prop) { Color = Color.green })) {
                        GL.Begin(GL.LINES);
                        GL.Vertex(search);
                        GL.Vertex(boundary);
                        GL.End();
                    }
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
    public class DataForDebug {
        public int index;
        public float2 totalForce;
        public float2 boundary_pos;
        public float2 center_pos;
        public float2 search_pos;
    }
    [System.Serializable]
    public class CharacterInfo {
        public Transform tr;
        public float2 wanderTarget;
    }
    [System.Serializable]
    public class Links {
        public Transform fab;
        public Transform fields;
    }

    [System.Serializable]
    public class Tuner {
        public int n = 1;
        public float speed;

        public float search_distance;

        public float boundary_power;
        public float boundary_width;

        public float wander_distance;
        public float wander_radius;
        public float wander_jitter;
    }
    #endregion
}