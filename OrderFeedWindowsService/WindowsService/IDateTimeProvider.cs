using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsService
{
    public interface IDateTimeProvider
    {
        DateTime Now { get; }
    }
}
