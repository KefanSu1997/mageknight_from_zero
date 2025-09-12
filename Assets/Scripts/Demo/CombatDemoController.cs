using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime;
using System.Linq;

namespace MK.Demo
{
    public class CombatDemoController : MonoBehaviour
    {
        [Header("UI References")]
        public Text combatLogText;
        public Text phaseIndicator;
        public Text attackerStats;
        public Text defenderStats;
        public Slider attackerHealth;
        public Slider defenderHealth;
        public Button nextPhaseButton;
        public Button resetCombatButton;
        public Transform damageNumbersParent;
        
        [Header("Visual Effects")]
        public GameObject damageNumberPrefab;
        public Color physicalDamageColor = Color.red;
        public Color fireDamageColor = new Color(1f, 0.5f, 0f); // 橙色，等同于Color.orange
        public Color iceDamageColor = Color.cyan;
        
        // 移除BattleResolver实例变量，因为BattleResolver是静态类
        private class DemoUnit
        {
            public string Name { get; set; }
            public int Health { get; set; }
            public int Attack { get; set; }
            public int Defense { get; set; }
            public List<Ability> Abilities { get; set; } = new();
            
            public DemoUnit(string name, int health, int attack, int defense)
            {
                Name = name;
                Health = health;
                Attack = attack;
                Defense = defense;
            }
            
            public void TakeDamage(int damage)
            {
                Health = Mathf.Max(0, Health - damage);
            }
            
            public void AddAbility(Ability ability)
            {
                Abilities.Add(ability);
            }
            
            public bool HasAbility(Ability ability)
            {
                return Abilities.Contains(ability);
            }
        }
        
        private DemoUnit _attacker;
        private DemoUnit _defender;
        private CombatPhase _currentPhase = CombatPhase.None;
        private List<string> _combatLog = new List<string>();
        
        public enum CombatPhase
        {
            None,
            Ranged,
            Block,
            AssignDamage,
            Melee,
            End
        }
        
        void Start()
        {
            SetupDemoBattle();
            UpdateUI();
            
            nextPhaseButton.onClick.AddListener(AdvancePhase);
            resetCombatButton.onClick.AddListener(ResetCombat);
        }
        
        void SetupDemoBattle()
        {
            // 创建测试单位
            _attacker = new DemoUnit("战士", 10, 5, 3);
            _attacker.AddAbility(Ability.Brutal);
            
            _defender = new DemoUnit("怪物", 8, 4, 2);
            _defender.AddAbility(Ability.Fortified);
            
            Log("战斗开始！");
            Log($"攻击者: {_attacker.Name} (血量: {_attacker.Health}, 攻击: {_attacker.Attack}, 防御: {_attacker.Defense})");
            Log($"防御者: {_defender.Name} (血量: {_defender.Health}, 攻击: {_defender.Attack}, 防御: {_defender.Defense})");
        }
        
        void AdvancePhase()
        {
            switch (_currentPhase)
            {
                case CombatPhase.None:
                    _currentPhase = CombatPhase.Ranged;
                    ExecuteRangedPhase();
                    break;
                case CombatPhase.Ranged:
                    _currentPhase = CombatPhase.Block;
                    ExecuteBlockPhase();
                    break;
                case CombatPhase.Block:
                    _currentPhase = CombatPhase.AssignDamage;
                    ExecuteAssignDamagePhase();
                    break;
                case CombatPhase.AssignDamage:
                    _currentPhase = CombatPhase.Melee;
                    ExecuteMeleePhase();
                    break;
                case CombatPhase.Melee:
                    _currentPhase = CombatPhase.End;
                    ExecuteEndPhase();
                    break;
                case CombatPhase.End:
                    ResetCombat();
                    break;
            }
            
            UpdateUI();
        }
        
        void ExecuteRangedPhase()
        {
            Log("=== 远程阶段 ===");
            
            // 模拟远程攻击
            var rangedAttack = new DamagePacket(Element.Physical, _attacker.Attack);
            var damage = CalculateDemoDamage(rangedAttack, _defender);
            
            if (damage > 0)
            {
                ShowDamageNumber(damage, Element.Physical);
                _defender.TakeDamage(damage);
                Log($"远程攻击造成 {damage} 点伤害！");
            }
            else
            {
                Log("远程攻击被完全阻挡！");
            }
        }
        
        void ExecuteBlockPhase()
        {
            Log("=== 阻挡阶段 ===");
            
            // 模拟防御方阻挡
            int blockAmount = _defender.Defense;
            Log($"防御方使用 {blockAmount} 点防御力进行阻挡");
            
            if (_defender.HasAbility(Ability.Fortified))
            {
                Log("防御方拥有'加固'能力，阻挡+2！");
                blockAmount += 2;
            }
        }
        
        void ExecuteAssignDamagePhase()
        {
            Log("=== 伤害分配阶段 ===");
            
            // 模拟伤害分配
            var totalDamage = _attacker.Attack;
            Log($"需要分配 {totalDamage} 点伤害");
            
            // 分配到防御方
            _defender.TakeDamage(totalDamage);
            ShowDamageNumber(totalDamage, Element.Physical);
            Log($"防御方受到 {totalDamage} 点伤害！");
        }
        
        void ExecuteMeleePhase()
        {
            Log("=== 近战阶段 ===");
            
            // 模拟近战攻击
            var meleeAttack = new DamagePacket(Element.Physical, _attacker.Attack + 2);
            var damage = CalculateDemoDamage(meleeAttack, _defender);
            
            if (damage > 0)
            {
                ShowDamageNumber(damage, Element.Physical);
                _defender.TakeDamage(damage);
                Log($"近战攻击造成 {damage} 点伤害！");
            }
            
            // 防御方反击
            if (_defender.Health > 0)
            {
                var counterAttack = new DamagePacket(Element.Physical, _defender.Attack);
                var counterDamage = CalculateDemoDamage(counterAttack, _attacker);
                
                if (counterDamage > 0)
                {
                    ShowDamageNumber(counterDamage, Element.Physical);
                    _attacker.TakeDamage(counterDamage);
                    Log($"防御方反击造成 {counterDamage} 点伤害！");
                }
            }
        }
        
        void ExecuteEndPhase()
        {
            Log("=== 战斗结束 ===");
            
            if (_attacker.Health <= 0)
            {
                Log($"攻击者 {_attacker.Name} 被击败！");
            }
            else if (_defender.Health <= 0)
            {
                Log($"防御者 {_defender.Name} 被击败！");
            }
            else
            {
                Log("双方仍然存活，战斗继续...");
            }
            
            Log($"最终状态 - 攻击者血量: {_attacker.Health}, 防御者血量: {_defender.Health}");
        }
        
        int CalculateDemoDamage(DamagePacket damage, DemoUnit target)
        {
            int baseDamage = damage.Amount;
            
            // 根据元素类型调整伤害
            switch (damage.Element)
            {
                case Element.Fire:
                    if (target.HasAbility(Ability.IceResist))
                        baseDamage = Mathf.Max(1, baseDamage - 2);
                    break;
                case Element.Ice:
                    if (target.HasAbility(Ability.FireResist))
                        baseDamage = Mathf.Max(1, baseDamage - 2);
                    break;
            }
            
            // 防御减免
            int finalDamage = Mathf.Max(0, baseDamage - target.Defense);
            return finalDamage;
        }
        
        void ShowDamageNumber(int damage, Element element)
        {
            if (damageNumberPrefab != null)
            {
                var damageObj = Instantiate(damageNumberPrefab, damageNumbersParent);
                var damageText = damageObj.GetComponent<Text>();
                if (damageText != null)
                {
                    damageText.text = $"-{damage}";
                    
                    // 根据元素类型设置颜色
                    switch (element)
                    {
                        case Element.Physical:
                            damageText.color = physicalDamageColor;
                            break;
                        case Element.Fire:
                            damageText.color = fireDamageColor;
                            break;
                        case Element.Ice:
                            damageText.color = iceDamageColor;
                            break;
                    }
                }
                
                // 添加动画效果
                var animator = damageObj.GetComponent<Animation>();
                if (animator != null)
                {
                    animator.Play();
                }
                
                Destroy(damageObj, 2f);
            }
        }
        
        void UpdateUI()
        {
            // 更新阶段指示器
            phaseIndicator.text = $"当前阶段: {_currentPhase}";
            
            // 更新单位状态
            attackerStats.text = $"攻击者\n血量: {_attacker.Health}\n攻击: {_attacker.Attack}\n防御: {_attacker.Defense}";
            defenderStats.text = $"防御者\n血量: {_defender.Health}\n攻击: {_defender.Attack}\n防御: {_defender.Defense}";
            
            // 更新血条
            attackerHealth.maxValue = 10;
            attackerHealth.value = _attacker.Health;
            defenderHealth.maxValue = 8;
            defenderHealth.value = _defender.Health;
            
            // 更新战斗日志
            var recentLogs = _combatLog.Count > 10 ? _combatLog.Skip(_combatLog.Count - 10).ToList() : _combatLog;
            combatLogText.text = string.Join("\n", recentLogs);
        }
        
        void Log(string message)
        {
            _combatLog.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            Debug.Log(message);
        }
        
        void ResetCombat()
        {
            _currentPhase = CombatPhase.None;
            _combatLog.Clear();
            SetupDemoBattle();
            UpdateUI();
            
            // 清理伤害数字
            foreach (Transform child in damageNumbersParent)
            {
                Destroy(child.gameObject);
            }
        }
    }
}