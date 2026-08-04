namespace HanabePhotoManager.App.WeChat;

public static class WeChatBatchPlanner
{
    public static IReadOnlyList<WeChatSendBatch> Create(IReadOnlyList<WeChatSendItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var remaining = new Queue<WeChatSendItem>(items);
        var batches = new List<WeChatSendBatch>();

        while (remaining.Count > 0)
        {
            var batch = new List<WeChatSendItem>(9);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deferred = new Queue<WeChatSendItem>();

            while (remaining.Count > 0 && batch.Count < 9)
            {
                var item = remaining.Dequeue();
                if (names.Add(item.DisplayName))
                    batch.Add(item);
                else
                    deferred.Enqueue(item);
            }

            while (remaining.Count > 0)
                deferred.Enqueue(remaining.Dequeue());

            batches.Add(new WeChatSendBatch(batches.Count + 1, batch));
            remaining = deferred;
        }

        return batches;
    }
}
