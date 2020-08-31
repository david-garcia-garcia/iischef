using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace iischef.utils
{
    public class CustomContractResolver : DefaultContractResolver
    {
        public bool Skip = false;

        public CustomContractResolver()
        {
        }

        public override JsonContract ResolveContract(Type type)
        {
            var contract = base.ResolveContract(type);
            
            // Very careful here... contract resolvers and converters are stored in an internal
            // cache.... so ResolveContractConverter() will not always get called once
            // the resolution is cached. What we are going to do is override the converter
            // very time.
            contract.Converter = this.ResolveContractConverter(type);

            if (this.Skip)
            {
                contract.Converter = null;
                this.Skip = false;
            }

            return contract;
        }

        protected override JsonArrayContract CreateArrayContract(Type objectType)
        {
            return base.CreateArrayContract(objectType);
        }

        protected override JsonProperty CreateProperty(
            MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => (shouldSerialize == null ||
                                               shouldSerialize(obj));
            return property;
        }

        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            var converter = base.ResolveContractConverter(objectType);

            if (typeof(IDictionary).IsAssignableFrom(objectType) && !this.Skip)
            {
                converter = new ArrayToDictionaryConverter();
            }

            return converter;
        }
    }
}
