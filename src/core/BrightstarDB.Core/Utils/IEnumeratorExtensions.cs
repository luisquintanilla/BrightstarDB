using System.Collections.Generic;

namespace BrightstarDB.Utils
{

    internal static class IEnumeratorExtensions
    {
        /// <summary>
        /// Retrieves up to <paramref name="max"/> elements from the enumerator
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerator"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static List<T> Next<T>(this IEnumerator<T> enumerator, int max)
        {
            var result = new List<T>(max);
            while (result.Count < max && enumerator.MoveNext())
            {
                result.Add(enumerator.Current);
            }
            return result;
        }
    }
}
