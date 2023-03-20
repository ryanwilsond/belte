using System;
using System.Collections.Generic;

namespace a;

public static class Program {

    public static void Main() {
        List<Nullable<int>> a = new List<Nullable<int>> { 1, 2, 3 };
        Nullable<int> b = a[0];
        List<object> c = new List<object> { 1, 3.3, false };
        Nullable<int> d = (Nullable<int>)Convert.ToInt32(c[0]);
        b++;
        --d;
        Nullable<int> x = 4;
        ref Nullable<int> y = ref x;
        x++;
        Console.WriteLine((object)y);
        a[1] = 4;
        Console.WriteLine((object)a[1]);
        Nullable<int> z = 3;
        int g = z.Value;
        return;
    }

}
