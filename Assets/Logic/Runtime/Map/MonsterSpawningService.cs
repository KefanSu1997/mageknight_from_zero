using MK.Logic.Core;
using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 怪物生成服务，负责在地图位置生成各类怪物
    /// </summary>
    public sealed class MonsterSpawningService
    {
        private readonly Random _random;

        public MonsterSpawningService(Random? random = null)
        {
            _random = random ?? new Random();
        }

        /// <summary>
        /// 根据地形类型随机生成对应怪物
        /// </summary>
        public Monster SpawnRandomMonster(AxialCoord coord)
        {
            return new Monster(MonsterType.Wandering, coord);
        }

        /// <summary>
        /// 生成特定类型的怪物于指定位置
        /// </summary>
        public Monster SpawnMonster(MonsterType type, AxialCoord coord)
        {
            return new Monster(type, coord);
        }
    }

    /// <summary>
    /// 怪物数据类
    /// </summary>
    public sealed class Monster
    {
        public MonsterType Type { get; }
        public AxialCoord Position { get; }
        public int Hull { get; init; } = 1;
        public int Force { get; init; } = 1;

        public Monster(MonsterType type, AxialCoord position)
        {
            Type = type;
            Position = position;
        }
    }

    /// <summary>
    /// 怪物类型枚举
    /// </summary>
    public enum MonsterType
    {
        Orc,
        Dragon,
        Demon,
        IceDragon,
        FireDragon,
        Skeleton,
        Gargoyle,
        Minotaur,
        Chimera,
        Wandering
    }

    /// <summary>
    /// 建筑类型枚举
    /// </summary>
    public enum BuildingType
    {
        Monastery,
        City,
        Keep,
        Village,
        MageTower,
        Dungeon,
        Ruins
    }

    /// <summary>
    /// 地块类型枚举
    /// </summary>
    public enum TileType
    {
        Forest,
        Desert,
        Mountain,
        Swamp,
        Plains,
        Hills,
        AncientRuins,
        Monastery,
        City,
        Keep,
        Village
    }

    /// <summary>
    /// SpawnedElement实用扩展方法
    /// </summary>
    public static class SpawnedElementExtensions
    {
        public static SpawnedElement Monster(Monster monster) => 
            new SpawnedElement("Monster", monster, monster.Position);
        
        public static SpawnedElement Building(BuildingType buildingType, AxialCoord coord) => 
            new SpawnedElement("Building", buildingType, coord);
        
        public static SpawnedElement AncientLocation(AxialCoord coord) => 
            new SpawnedElement("AncientLocation", "AncientRuin", coord);
    }
}