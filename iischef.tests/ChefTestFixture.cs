using System;
using iischef.core;

namespace healthmonitortests
{
    public class ChefTestFixture
    {
        public ChefTestFixture()
        {
            BindingRedirectHandler.DoBindingRedirects(AppDomain.CurrentDomain);
        }
    }
}
