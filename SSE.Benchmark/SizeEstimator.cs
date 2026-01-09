using System.Collections;
using System.Numerics;
using System.Reflection;

namespace SSE.Benchmark
{
    public static class SizeEstimator
    {
        public static long EstimateSize(object? obj, HashSet<object> visited = null)
        {
            if (obj == null) return 0;
            visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            
            if (visited.Contains(obj)) return 0;
            visited.Add(obj);

            Type type = obj.GetType();

            if (type.IsPrimitive || type.IsEnum) return System.Runtime.InteropServices.Marshal.SizeOf(type);
            if (obj is string s) return s.Length * 2 + 20; // Approx overhead
            if (type == typeof(BigInteger)) return ((BigInteger)obj).GetByteCount();
            if (obj is byte[] b) return b.Length + 16; // Array overhead

            // If it's a collection, we can mostly rely on reflection of its backing fields (e.g. _items, _entries)
            // to capture the data. Iterating via IEnumerable usually generates copies or structs that 
            // might lead to double counting if we also reflect fields.
            // However, we need to be careful about strict dependencies.
            // For standard collections (List, Dictionary), reflection covers the internal arrays.
            
            // Special handling for Strings, BigInteger, Arrays are done above.
            
            long size = 0;

            // What if it is a primitive array? e.g. int[]
            // Arrays are IEnumerable.
            if (type.IsArray)
            {
               Array arr = (Array)obj;
               Type elemType = type.GetElementType();
               if (elemType.IsPrimitive) return arr.Length * System.Runtime.InteropServices.Marshal.SizeOf(elemType) + 16;
               // If array of references, we need to iterate
               long arrSize = 16;
               foreach (var item in arr)
               {
                   arrSize += EstimateSize(item, visited); // Size of reference (ptr) is handled? 
                   // Wait, pointer size is part of the array structure. 
                   // If I have Object[], it has N pointers. Marshal.SizeOf doesn't work for Object[].
                   // We just accept we count the object graph. 
                   // We should add pointer size (4 or 8 bytes) per element.
                   arrSize += IntPtr.Size; 
               }
               return arrSize;
            }

            // Reflect fields
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.IsStatic) continue;
                
                // If it's a value type, SizeOf handles it mostly, but for struct layout... 
                // Let's simplified assumption:
                var val = field.GetValue(obj);
                if (field.FieldType.IsValueType && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType != typeof(BigInteger)) 
                {
                    // struct
                    size += EstimateSize(val, visited); 
                }
                else
                {
                    // Reference or primitive boxed
                   size += EstimateSize(val, visited);
                }
            }
            
            return size;
        }
    }
}
