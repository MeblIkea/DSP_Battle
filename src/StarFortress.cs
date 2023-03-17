﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DSP_Battle
{
    public class StarFortress
    {
        public static List<int> moduleCapacity;
        public static List<List<int>> moduleComponentCount; // 存储已完成的组件数，除以每模块的组件需求数量就是已经完成的模块数
        public static List<List<int>> moduleMaxCount; // 存储规划的模块数

        static int lockLoop = 0; // 为减少射击弹道飞行过程中重复锁定同一个敌人导致伤害溢出的浪费，恒星要塞的炮会依次序攻击队列中第lockLoop序号的敌人，且每次攻击后此值+1（对一个循环上限取余，循环上线取决于射击频率，原则上射击频率越快循环上限越大，循环上限loop通过FireCannon函数传入）

        public static void InitAll()
        {
            StarFortressSilo.InitAll();
            UIStarFortress.InitAll();
            moduleCapacity = new List<int>(1000);
            moduleComponentCount = new List<List<int>>();
            moduleMaxCount = new List<List<int>>();
            for (int i = 0; i < 1000; i++)
            {
                moduleComponentCount.Add(new List<int> { 0, 0, 0, 0 });
                moduleMaxCount.Add(new List<int> { 0, 0, 0, 0 });
            }
        }

        public static bool NeedRocket(DysonSphere sphere, int rocketId)
        {

            return true;
        }

        public static void ConstructStarFortPoint(int starIndex, int rocketProtoId)
        { 
        
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DysonSphere), "GameTick")]
        public static void StarFortressGameTick(ref DysonSphere __instance, long gameTick)
        {
            int starIndex = __instance.starData.index;

            if (Configs.nextWaveState == 3  && starIndex == Configs.nextWaveStarIndex)
            {
                if(gameTick % 1 == 0) // 测试时暂时固定
                    FireCannon(ref __instance.swarm);
                if (gameTick % 60 == 0)
                {
                    System.Random rand = new System.Random();

                    // 发射导弹数量，测试时固定
                    for (int i = 0; i < 10; i++)
                    {
                        DysonNode node = null;
                        int beginLayerIndex = rand.Next(1, 10);
                        // 寻找第一个壳面
                        for (int layerIndex = beginLayerIndex; layerIndex < 10; layerIndex = (layerIndex + 1) % 10)
                        {
                            if (__instance.layersIdBased.Length > layerIndex && __instance.layersIdBased[layerIndex] != null && __instance.layersIdBased[layerIndex].nodeCount > 0)
                            {
                                DysonSphereLayer layer = __instance.layersIdBased[layerIndex];
                                bool found = false; // 寻找到可用的发射node之后，发射导弹，一直break到外面
                                int beginNodeIndex = rand.Next(0, Math.Max(1, layer.nodePool.Length));
                                for (int nodeIndex = beginNodeIndex; nodeIndex < layer.nodeCursor && nodeIndex < layer.nodeCursor; nodeIndex++)
                                {
                                    if (layer.nodePool[nodeIndex] != null)
                                    {
                                        found = true;
                                        LauchMissile(layer, layer.nodePool[nodeIndex]);
                                        break;
                                    }
                                }
                                if (found)
                                    break;
                                for (int nodeIndex = 0; nodeIndex < beginNodeIndex; nodeIndex++)
                                {
                                    if (layer.nodePool[nodeIndex] != null)
                                    {
                                        found = true;
                                        LauchMissile(layer, layer.nodePool[nodeIndex]);
                                        break;
                                    }
                                }
                                if (found)
                                    break;
                            }

                            if (layerIndex == beginLayerIndex - 1) break;
                        }
                    }
                }
            }
        }

        public static void LauchMissile(DysonSphereLayer layer, DysonNode node)
        {
            if (node == null) return;
            StarData star = layer.starData;
            int starIndex = star.index;
            Vector3 nodeUPos = layer.NodeUPos(node);
            Vector3 starUPos = star.uPosition;
            int targetIndex = MissileSilo.FindTarget(starIndex, star.id * 100 + 1);

            DysonRocket dysonRocket = default(DysonRocket);
            dysonRocket.planetId = star.id * 100 + 1;
            dysonRocket.uPos = nodeUPos;
            dysonRocket.uRot = Quaternion.LookRotation(nodeUPos-starUPos, new Vector3(0,1,0));
            dysonRocket.uVel = dysonRocket.uRot * Vector3.forward;
            dysonRocket.uSpeed = 0f;
            dysonRocket.launch = (nodeUPos-starUPos).normalized;
            //sphere.AddDysonRocket(dysonRocket, autoDysonNode); //原本
            int rocketIndex = MissileSilo.AddDysonRockedGniMaerd(ref layer.dysonSphere, ref dysonRocket, null); //这是添加了一个目标戴森球节点为null的火箭，因此被判定为导弹

            MissileSilo.MissileTargets[starIndex][rocketIndex] = targetIndex;
            MissileSilo.missileProtoIds[starIndex][rocketIndex] = 8008; // 虽然伤害是按照反物质导弹的但是序号是8008不计入导弹统计
            //int damage = 0;
            //if (__instance.bulletId == 8004) damage = Configs.missile1Atk;
            //else if (__instance.bulletId == 8005) damage = Configs.missile2Atk;
            //else if (__instance.bulletId == 8006) damage = Configs.missile3Atk;
            ////注册导弹
            //UIBattleStatistics.RegisterShootOrLaunch(__instance.bulletId, damage);
        }

        public static void FireCannon(ref DysonSwarm swarm, int loop = 20)
        {
            lockLoop = (lockLoop + 1) % loop;
            int starIndex = Configs.nextWaveStarIndex;
            StarData star = swarm.starData;

            try // 仅在快结束战斗时可能会越界报错，推测是sorted的问题。
            {
                List<EnemyShip> sortedShips = EnemyShips.sortedShips(1, starIndex, starIndex * 100 + 101);
                int targetIndex = MissileSilo.FindTarget(starIndex, starIndex * 100 + 101);
                int bulletIndex;
                if (targetIndex < 0) return;
                EnemyShip enemyShip = sortedShips[Math.Min(lockLoop, sortedShips.Count)];
                Vector3 targetUPos = star.uPosition;
                if (enemyShip != null && enemyShip.state == EnemyShip.State.active)
                    targetUPos = enemyShip.uPos;
                else
                    return;
                float t = (float)((VectorLF3)targetUPos - star.uPosition).magnitude / 250000f;

                //下面是添加子弹
                for (int i = 0; i < 10; i++)
                {
                    VectorLF3 randDelta = Utils.RandPosDelta();
                    bulletIndex = swarm.AddBullet(new SailBullet
                    {
                        maxt = t + i * 0.01f,
                        lBegin = star.uPosition,
                        uEndVel = targetUPos, //至少影响着形成的太阳帆的初速度方向
                        uBegin = star.uPosition + randDelta,
                        uEnd = (VectorLF3)targetUPos + randDelta
                    }, 2);

                    try
                    {
                        if (bulletIndex != -1)
                            swarm.bulletPool[bulletIndex].state = 0; //设置成0，该子弹将不会生成太阳帆
                    }
                    catch (Exception)
                    {
                        DspBattlePlugin.logger.LogInfo("bullet info1 set error.");
                    }

                    if (bulletIndex != -1)
                        Cannon.bulletTargets[swarm.starData.index].AddOrUpdate(bulletIndex, enemyShip.shipIndex, (x, y) => enemyShip.shipIndex);

                    //Main.logger.LogInfo("bullet info2 set error.");


                    try
                    {
                        int bulletId = 8009;
                        if (bulletIndex != -1)
                            Cannon.bulletIds[swarm.starData.index].AddOrUpdate(bulletIndex, bulletId, (x, y) => bulletId);
                        // bulletIds[swarm.starData.index][bulletIndex] = 1;//后续可以根据子弹类型/炮类型设定不同数值
                    }
                    catch (Exception)
                    {
                        DspBattlePlugin.logger.LogInfo("bullet info8009 set error.");
                    }
                }
            }
            catch (Exception)
            {

            }
        }


        public static void Export(BinaryWriter w)
        {
            
        }

        public static void Import(BinaryReader r)
        {
            InitAll();
        }

        public static void IntoOtherSave()
        {
            InitAll();
        }
    }
}
