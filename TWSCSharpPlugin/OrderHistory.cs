using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RightEdge.Common;

namespace RightEdge.TWSCSharpPlugin
{
    class OrderHistory
    {
        Queue<HistoricalOrder> _orderHistoryQueue = new Queue<HistoricalOrder>();
        Dictionary<string, HistoricalOrder> _orderHistory = new Dictionary<string, HistoricalOrder>();

        public void RecordOrder(string orderId, DateTime time, BrokerOrderState orderState)
        {
            HistoricalOrder histOrder = new HistoricalOrder() { OrderId = orderId, FilledTime = time, OrderState = orderState };
            _orderHistory[orderId] = histOrder;
            _orderHistoryQueue.Enqueue(histOrder);

            Trim(time);
        }

        public bool WasRecorded(string orderId)
        {
            return _orderHistory.ContainsKey(orderId);
        }

        public bool TryGetOrderState(string orderId, out BrokerOrderState orderState)
        {
            orderState = BrokerOrderState.Submitted;
            HistoricalOrder histOrder;
            if (_orderHistory.TryGetValue(orderId, out histOrder))
            {
                orderState = histOrder.OrderState;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void Trim(DateTime currentTime)
        {
            if (_orderHistoryQueue.Count == 0)
            {
                return;
            }
            while (currentTime.Subtract(_orderHistoryQueue.Peek().FilledTime).TotalMinutes > 1)
            {
                _orderHistory.Remove(_orderHistoryQueue.Peek().OrderId);
                _orderHistoryQueue.Dequeue();
            }
        }

        class HistoricalOrder
        {
            public string OrderId { get; set; }
            public DateTime FilledTime { get; set; }
            public BrokerOrderState OrderState { get; set; }
        }
    }
}
