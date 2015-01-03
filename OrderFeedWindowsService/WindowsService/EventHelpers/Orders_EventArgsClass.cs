using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsService.EventHelpers
{
    public class OrdersEventArgsClass : EventArgs
    {
        public int OrderId { get; set; }
        public string CustomerId { get; set; }

        public OrdersEventArgsClass(int orderId, string customerId)
        {
            OrderId = orderId;
            CustomerId = customerId;
        }
    }
}
