using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>一局里固定的一个竖向核（西梯 / 核心筒电梯 / 东梯）。</summary>
    public struct VerticalCore
    {
        /// <summary>0 = 西，1 = 中，2 = 东。验收要求至少跨 2 个象限。</summary>
        public int Column;
        public VerticalLinkKind Kind;
        public RectF Rect;

        public Vector2 Center { get { return Rect.Center; } }
    }

    /// <summary>
    /// R6：一局里所有楼层共用同一套竖向核坐标。
    /// 层与层之间不复算核心筒/楼梯位置，而是由这里统一下发，所以"跨层对齐"是结构保证，
    /// 不是"算出来恰好相同"——误差上限 0.5m（RoundTo 到 0.1m 后完全一致）。
    /// </summary>
    public class VerticalLinkPlan
    {
        /// <summary>验收容差：相邻层同一竖向核的中心偏差必须小于这个值。</summary>
        public const float AlignTolerance = 0.5f;

        public List<VerticalCore> Cores = new List<VerticalCore>();

        /// <summary>按某一层的规格算出标准竖向核布局（与 FloorGenerator 的默认布局一致）。</summary>
        public static VerticalLinkPlan Default(FloorSpec spec)
        {
            VerticalLinkPlan plan = new VerticalLinkPlan();

            RectF core = new RectF(
                spec.Width * 0.5f - spec.CoreSize.x * 0.5f,
                spec.Height - spec.CoreNorthOffset - spec.CoreSize.y,
                spec.CoreSize.x,
                spec.CoreSize.y);
            plan.Cores.Add(new VerticalCore { Column = 1, Kind = VerticalLinkKind.Elevator, Rect = core });

            float stairY = spec.Height * 0.5f - 10f;
            plan.Cores.Add(new VerticalCore
            {
                Column = 0,
                Kind = VerticalLinkKind.Stair,
                Rect = new RectF(2f, stairY, 8f, 20f),
            });
            plan.Cores.Add(new VerticalCore
            {
                Column = 2,
                Kind = VerticalLinkKind.Stair,
                Rect = new RectF(spec.Width - 10f, stairY, 8f, 20f),
            });
            return plan;
        }

        /// <summary>
        /// 把整局所有核在平面上做一次统一平移（一局之内整体偏移，层与层之间仍完全对齐）。
        /// 用途：同一张种子在不同局之间看起来不完全一样，但不会破坏竖向对位。
        /// </summary>
        public VerticalLinkPlan Shifted(Vector2 delta)
        {
            VerticalLinkPlan shifted = new VerticalLinkPlan();
            for (int i = 0; i < Cores.Count; i++)
            {
                VerticalCore c = Cores[i];
                c.Rect = new RectF(c.Rect.X + delta.x, c.Rect.Y + delta.y, c.Rect.W, c.Rect.H);
                shifted.Cores.Add(c);
            }
            return shifted;
        }

        /// <summary>旧 Lab 描述符的兼容投影；R11 Raid 不得使用这个跨层对齐楼梯模型。</summary>
        [System.Obsolete("R11 raid uses StairGraphPlan; this adapter is only for legacy Lab tooling.")]
        public void ApplyTo(FloorSpec spec, int floorIndex)
        {
            spec.UseStairGraph = true;
            spec.StairRects.Clear();
            for (int i = 0; i < Cores.Count; i++)
            {
                VerticalCore c = Cores[i];
                if (c.Kind == VerticalLinkKind.Elevator) spec.CoreRect = c.Rect;
                if (c.Kind == VerticalLinkKind.Stair) spec.StairRects.Add(c.Rect);
            }
        }

        /// <summary>同一种子在两局里共享同一套核坐标时，用来做逐层校验的稳定指纹。</summary>
        public string Fingerprint()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Cores.Count; i++)
            {
                VerticalCore c = Cores[i];
                sb.Append(c.Column).Append(':')
                  .Append(c.Kind).Append(':')
                  .Append(c.Rect.X.ToString("0.0")).Append(',')
                  .Append(c.Rect.Y.ToString("0.0")).Append(',')
                  .Append(c.Rect.W.ToString("0.0")).Append('x')
                  .Append(c.Rect.H.ToString("0.0")).Append(';');
            }
            return sb.ToString();
        }
    }
}
