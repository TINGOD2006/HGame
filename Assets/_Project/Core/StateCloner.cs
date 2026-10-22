using System;
using System.Collections;
using System.Reflection;

namespace HGame.Core
{
    public static class StateCloner
    {
        public static T Clone<T>(T value) { return (T)Copy(value); }
        public static RunState Run(RunState value) { return Clone(value); }
        public static MetaState Meta(MetaState value) { return Clone(value); }
        public static ProfileState Profile(ProfileState value) { return Clone(value); }
        public static GameSnapshot Snapshot(GameSnapshot value) { return Clone(value); }
        private static object Copy(object value)
        {
            if (value == null) return null;
            Type type = value.GetType();
            if (type.IsValueType || type == typeof(string)) return value;
            var list = value as IList;
            if (list != null)
            {
                var result = (IList)Activator.CreateInstance(type);
                foreach (object item in list) result.Add(Copy(item));
                return result;
            }
            object clone = Activator.CreateInstance(type);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                field.SetValue(clone, Copy(field.GetValue(value)));
            return clone;
        }
    }
}
