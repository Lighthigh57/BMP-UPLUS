﻿using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using E7.ECS.LineRenderer;

namespace BananaBeats.Visualization {
    public static class NoteLaneManager {
        private static readonly Lazy<EntityArchetype> noteLaneArchetype = new Lazy<EntityArchetype>(
            () => NoteDisplayManager.World.EntityManager.CreateArchetype(
                typeof(LineSegment),
                typeof(LineStyle),
                typeof(NoteLane)
            )
        );

        public static Material LaneMaterial { get; set; }

        public static Material GaugeMaterial { get; set; }

        public static Gradient LaneBeatFlowGradient { get; set; }

        public static float LaneLineWidth { get; set; } = 0.1F;

        public static float LaneGaugeAnimSpeed { get; set; } = 10F;

        public static void CreateLane(Vector3 pos1, Vector3 pos2) =>
            InternalCreate(pos1, pos2, LaneMaterial);

        public static void CreateGauge(Vector3 pos1, Vector3 pos2) {
            var entityManager = NoteDisplayManager.World.EntityManager;
            var instance = InternalCreate(pos1, pos2, GaugeMaterial, entityManager);
            entityManager.AddComponentData(instance, new NoteLaneLerp {
                timeScale = LaneGaugeAnimSpeed,
                maxValue = pos2,
            });
        }

        private static Entity InternalCreate(Vector3 pos1, Vector3 pos2, Material material, EntityManager entityManager = null) {
            if(entityManager == null)
                entityManager = NoteDisplayManager.World.EntityManager;
            var instance = entityManager.CreateEntity(noteLaneArchetype.Value);
            entityManager.SetComponentData(instance, new LineSegment {
                from = pos1,
                to = pos2,
                lineWidth = LaneLineWidth,
            });
            entityManager.SetSharedComponentData(instance, new LineStyle {
                material = material,
            });
            return instance;
        }

        public static void Clear() {
            var entityManager = NoteDisplayManager.World.EntityManager;
            if(entityManager != null && entityManager.IsCreated)
                entityManager.DestroyEntity(entityManager.CreateEntityQuery(typeof(NoteLane)));
        }

        public static void SetBeatFlowEffect(float value) {
            if(LaneMaterial == null || LaneBeatFlowGradient == null)
                return;
            LaneMaterial.color = LaneBeatFlowGradient.Evaluate(value);
        }
    }

    public class NoteLaneLerpSystem: JobComponentSystem {
        BeginInitializationEntityCommandBufferSystem cmdBufSystem;
        protected override void OnCreate() {
            cmdBufSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        [RequireComponentTag(typeof(NoteLane))]
        private struct Job: IJobForEachWithEntity<NoteLaneLerp, LineSegment> {
            public float time;
            public EntityCommandBuffer.Concurrent cmdBuffer;

            public void Execute(Entity entity, int index, ref NoteLaneLerp lerp, ref LineSegment seg) {
                lerp.value += time * lerp.timeScale;
                seg.to = math.lerp(seg.from, lerp.maxValue, math.min(1, lerp.value));
                if(lerp.value >= 1) cmdBuffer.RemoveComponent<NoteLaneLerp>(index, entity);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            var job = new Job {
                time = Time.deltaTime,
                cmdBuffer = cmdBufSystem.CreateCommandBuffer().ToConcurrent(),
            };
            inputDeps = job.Schedule(this, inputDeps);
            cmdBufSystem.AddJobHandleForProducer(inputDeps);
            return inputDeps;
        }
    }

    public struct NoteLane: IComponentData { }

    public struct NoteLaneLerp: IComponentData {
        public float value;
        public float3 maxValue;
        public float timeScale;
    }
}