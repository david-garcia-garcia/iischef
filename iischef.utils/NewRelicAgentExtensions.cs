namespace iischef.utils
{
    public static class NewRelicAgentExtensions
    {
        /// <summary>
        /// Se estaban guardando datos nulos y eso genera problemas en logs.
        /// Por lo que solamente se guarda en New Relic cuando el campo value no es nulo.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddCustomParameter(string key, string value)
        {
            if (!value.IsNullOrDefault())
            {
                NewRelic.Api.Agent.NewRelic.GetAgent()?.CurrentTransaction?.AddCustomAttribute(key, value);
            }
        }
    }
}
