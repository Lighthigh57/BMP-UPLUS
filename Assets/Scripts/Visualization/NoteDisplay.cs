﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

namespace BananaBeats.Visualization {
    public struct NoteDisplay: IComponentData {
        public int channel;
        public bool catched;
        public float pos;
        public float scale;
    }

    public struct LongNoteDisplay: IComponentData {
        public int channel;
        public bool catched;
        public float pos1;
        public float pos2;
        public float scale1;
        public float scale2;
    }

    public struct FadeOut: IComponentData {
        public float life;
    }

    public enum NoteType: byte {
        Normal,
        LongStart,
        LongBody,
        LongEnd,
        Fake,
    }

    public static class NoteDisplayManager {
        private struct NoteEntiyInstance {
            public Entity noteStart;
            public bool hasNoteEnd;
            public Entity noteEnd;
            public bool hasLongNoteBody;
            public Entity longNoteBody;
        }

        private static readonly Dictionary<NoteType, Entity> prefabs = new Dictionary<NoteType, Entity>();
        private static readonly Dictionary<int, NoteEntiyInstance> instances = new Dictionary<int, NoteEntiyInstance>();

        private static int nextId;

        private static World world;
        public static World World {
            get {
                if(world == null) world = World.Active;
                return world;
            }
            set { world = value ?? World.Active; }
        }

        public static void ConvertPrefab(GameObject prefab, NoteType noteType) {
            prefabs[noteType] = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefab, World);
        }

        public static int Spawn(int channel, TimeSpan time, NoteType noteType, float scale = 1) {
            var entityManager = World.EntityManager;
            var entityInstance = new NoteEntiyInstance();
            var pos = (float)time.Ticks / TimeSpan.TicksPerSecond;
            switch(noteType) {
                case NoteType.Normal:
                    entityInstance.noteStart = entityManager.Instantiate(prefabs[NoteType.Normal]);
                    break;
                case NoteType.LongStart:
                    entityInstance.noteStart = entityManager.Instantiate(prefabs[NoteType.LongStart]);
                    entityInstance.longNoteBody = entityManager.Instantiate(prefabs[NoteType.LongBody]);
                    entityInstance.hasLongNoteBody = true;
                    break;
                case NoteType.Fake:
                    entityInstance.noteStart = entityManager.Instantiate(prefabs[NoteType.Fake]);
                    break;
                default:
                    throw new ArgumentException("Invalid note type.", nameof(noteType));
            }
            entityManager.AddComponentData(entityInstance.noteStart, new NoteDisplay {
                channel = channel,
                pos = pos,
                scale = scale,
            });
            if(entityInstance.hasLongNoteBody) {
                entityManager.AddComponentData(entityInstance.longNoteBody, new NonUniformScale {
                    Value = new float3(1),
                });
                entityManager.AddComponentData(entityInstance.longNoteBody, new LongNoteDisplay {
                    channel = channel,
                    pos1 = pos,
                    pos2 = pos - 1,
                    scale1 = scale,
                });
            }
            int id = nextId++;
            instances[id] = entityInstance;
            return id;
        }

        public static void SetEndNoteTime(int id, TimeSpan time, float scale = 1) {
            if(!instances.TryGetValue(id, out var entiyInstance) || !entiyInstance.hasLongNoteBody || entiyInstance.hasNoteEnd)
                return;
            var entityManager = World.EntityManager;
            var data = entityManager.GetComponentData<LongNoteDisplay>(entiyInstance.longNoteBody);
            var pos = (float)time.Ticks / TimeSpan.TicksPerSecond;
            data.pos2 = pos;
            data.scale2 = scale;
            entityManager.SetComponentData(entiyInstance.longNoteBody, data);
            entiyInstance.noteEnd = entityManager.Instantiate(prefabs[NoteType.LongEnd]);
            entiyInstance.hasNoteEnd = true;
            entityManager.AddComponentData(entiyInstance.noteEnd, new NoteDisplay {
                channel = data.channel,
                pos = pos,
                scale = scale,
            });
            instances[id] = entiyInstance;
        }

        public static void HitNote(int id, bool isEnd) {
            if(!instances.TryGetValue(id, out var entiyInstance))
                return;
            var entityManager = World.EntityManager;
            NoteDisplay noteDisplay;
            LongNoteDisplay longNoteDisplay;
            {
                noteDisplay = entityManager.GetComponentData<NoteDisplay>(entiyInstance.noteStart);
                noteDisplay.catched = true;
                entityManager.SetComponentData(entiyInstance.noteStart, noteDisplay);
            }
            if(entiyInstance.hasLongNoteBody) {
                longNoteDisplay = entityManager.GetComponentData<LongNoteDisplay>(entiyInstance.longNoteBody);
                longNoteDisplay.catched = true;
                entityManager.SetComponentData(entiyInstance.longNoteBody, longNoteDisplay);
            }
            if(isEnd && entiyInstance.hasNoteEnd) {
                noteDisplay = entityManager.GetComponentData<NoteDisplay>(entiyInstance.noteEnd);
                noteDisplay.catched = true;
                entityManager.SetComponentData(entiyInstance.noteEnd, noteDisplay);
            }
        }

        public static void Destroy(int id) {
            if(!instances.TryGetValue(id, out var entiyInstance))
                return;
            var entityManager = World.EntityManager;
            SetFadeOut(ref entiyInstance.noteStart, entityManager);
            if(entiyInstance.hasLongNoteBody)
                SetFadeOut(ref entiyInstance.longNoteBody, entityManager);
            if(entiyInstance.hasNoteEnd)
                SetFadeOut(ref entiyInstance.noteEnd, entityManager);
            instances.Remove(id);
        }

        public static void Clear() {
            instances.Clear();
            var entityManager = World.EntityManager;
            entityManager.DestroyEntity(entityManager.CreateEntityQuery(typeof(NoteDisplay)));
            entityManager.DestroyEntity(entityManager.CreateEntityQuery(typeof(LongNoteDisplay)));
        }

        private static void SetFadeOut(ref Entity entity, EntityManager entityManager = null) {
            if(entityManager == null)
                entityManager = World.EntityManager;
            if(!entityManager.HasComponent<NonUniformScale>(entity))
                entityManager.AddComponentData(entity, new NonUniformScale { Value = new float3(1) });
            if(!entityManager.HasComponent<FadeOut>(entity))
                entityManager.AddComponent<FadeOut>(entity);
        }

        public static void RegisterPosition(Vector3[] refStartPos, Vector3[] refEndPos) {
            NoteDisplayScroll.refStartPos = Array.ConvertAll(refStartPos, V3toF3);
            NoteDisplayScroll.refEndPos = Array.ConvertAll(refEndPos, V3toF3);
        }

        private static float3 V3toF3(Vector3 vector3) => vector3;
    }
}