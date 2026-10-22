using System;
using System.Collections.Generic;
using System.Linq;

namespace HGame.Core
{
    public static class RewindRules
    {
        public static LoadPlan PlanLoad(RunState current, MetaState meta, RunState target, LoadIntent intent)
        {
            if (current == null || meta == null || target == null) throw new ArgumentNullException("存檔資料");
            var plan = new LoadPlan { SourceHeadId = current.TimelineHeadId, TargetHeadId = target.TimelineHeadId,
                CampaignSwitch = current.CampaignId != target.CampaignId };
            if (intent == LoadIntent.Continue || plan.CampaignSwitch || current.TimelineHeadId == target.TimelineHeadId) return plan;
            var targetAncestors = Ancestors(meta, target.TimelineHeadId);
            if (targetAncestors.Contains(current.TimelineHeadId)) return plan;
            string cursor = current.TimelineHeadId;
            var visited = new HashSet<string>();
            while (!string.IsNullOrEmpty(cursor) && !targetAncestors.Contains(cursor) && visited.Add(cursor))
            {
                var node = meta.Timeline.FirstOrDefault(x => x.Id == cursor);
                if (node == null) throw new InvalidOperationException("時間線資料不完整，無法安全讀檔。");
                if (!string.IsNullOrEmpty(node.ParentId)) plan.DiscardedNodeIds.Add(cursor);
                cursor = node.ParentId;
            }
            plan.IsRewind = plan.DiscardedNodeIds.Count > 0;
            if (plan.IsRewind)
                plan.ReceiptIds = meta.Receipts.Where(x => x.CampaignId == target.CampaignId && plan.DiscardedNodeIds.Contains(x.TimelineNodeId)
                    && !meta.ProcessedReceiptIds.Contains(x.Id)).Select(x => x.Id).ToList();
            return plan;
        }
        private static HashSet<string> Ancestors(MetaState meta, string head)
        {
            var result = new HashSet<string>(); string cursor = head;
            while (!string.IsNullOrEmpty(cursor) && result.Add(cursor))
            {
                var node = meta.Timeline.FirstOrDefault(x => x.Id == cursor);
                if (node == null) throw new InvalidOperationException("目標存檔的時間線資料不完整。");
                cursor = node.ParentId;
            }
            return result;
        }
        public static LoadOutcome ApplyLoad(LoadPlan plan, RunState target, MetaState meta, string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) throw new ArgumentException("讀檔交易需要識別碼。");
            if (meta.CampaignId != target.CampaignId) throw new InvalidOperationException("請使用目標旅程自己的記憶資料。");
            var resultMeta = StateCloner.Clone(meta);
            bool applied = resultMeta.AppliedTransactionIds.Contains(transactionId);
            if (!applied)
            {
                if (plan.IsRewind) resultMeta.EffectiveRewinds++;
                foreach (string id in plan.ReceiptIds)
                {
                    if (resultMeta.ProcessedReceiptIds.Contains(id)) continue;
                    var receipt = resultMeta.Receipts.FirstOrDefault(x => x.Id == id);
                    if (receipt == null) continue;
                    resultMeta.ProcessedReceiptIds.Add(id);
                    if (!resultMeta.SpecialMemoryIds.Contains(receipt.MemoryId)) resultMeta.SpecialMemoryIds.Add(receipt.MemoryId);
                }
                resultMeta.AppliedTransactionIds.Add(transactionId);
            }
            resultMeta.LastHeadId = target.TimelineHeadId;
            return new LoadOutcome { Run = StateCloner.Clone(target), Meta = resultMeta, IsRewind = plan.IsRewind,
                AlreadyApplied = applied, Message = plan.ReceiptIds.Count > 0 ? "讀取完成。一段來由模糊的記憶仍留在心中。" : "讀取完成。" };
        }
    }
}
