using System;
using System.Collections.Generic;

namespace a;

public static class Program {

    public class A {
        public Nullable<int> a;
        public Nullable<bool> b;
    }

    public static void Main() {
        A g = (A)new A();
        g.a = 5;
        Nullable<bool> c = g.b;
        Nullable<bool> d = (Nullable<bool>)(!c.HasValue);
        A h = null;
        Nullable<int> j = (((h) is not null) ? h.a : null);
        Console.WriteLine((object)(!j.HasValue));
        return;
    }

}
