using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsService.EventHelpers
{
    public class OrderWorkerClass
    {
        public event EventHandler<OrdersEventArgsClass> OrderWorkPerformed;

        public void DoWork(int orderId, string customerId)
        {
            OnOrderWorkPerformed(orderId, customerId);
        }

        private void OnOrderWorkPerformed(int orderId, string customerId)
        {
            var del = OrderWorkPerformed as EventHandler<OrdersEventArgsClass>;
            if (del != null)
            {
                del(this, new OrdersEventArgsClass(orderId,customerId));
            }
        }
    }
}
