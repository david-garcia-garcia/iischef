using System;
using iischef.core;

namespace iischeftests
{
    public class ChefTestFixture
    {
        public ChefTestFixture()
        {
            BindingRedirectHandler.DoBindingRedirects(AppDomain.CurrentDomain);
        }
    }
}
