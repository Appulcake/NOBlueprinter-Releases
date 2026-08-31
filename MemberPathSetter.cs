using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace Blueprinter
{
    public static class MemberPathSetter
    {
        // MemberPathSetter.TryApply(target, "someField.someList[2].otherField", asset);
        public static bool TryApply(object target, string memberPath, object asset)
        {
            if (target == null || string.IsNullOrEmpty(memberPath))
                return false;

            var path = ParseMemberPath(memberPath);
            if (path == null || path.Count == 0)
                return false;

            var current = target;
            for (int i = 0; i < path.Count - 1; i++)
            {
                current = GetSegmentValue(current, path[i]);
                if (current == null)
                    return false;
            }

            var lastSeg = path[path.Count - 1];
            if (lastSeg.Index.HasValue)
            {
                if (current is not IList list)
                    return false;

                int idx = lastSeg.Index.Value;
                if (idx < 0 || idx >= list.Count)
                    return false;

                list[idx] = asset;
                return true;
            }

            var t = Traverse.Create(current);
            var prop = t.Property(lastSeg.Name);
            if (prop.PropertyExists())
            {
                prop.SetValue(asset);
                return true;
            }

            var field = t.Field(lastSeg.Name);
            if (!field.FieldExists())
                return false;

            field.SetValue(asset);
            return true;
        }

        private struct Segment
        {
            public string Name;
            public int? Index;
        }

        private static List<Segment> ParseMemberPath(string path)
        {
            var segments = new List<Segment>();
            var sb = new StringBuilder();

            int i = 0;
            while (i < path.Length)
            {
                char c = path[i];

                if (c == '.')
                {
                    if (sb.Length > 0)
                    {
                        segments.Add(new Segment { Name = sb.ToString() });
                        sb.Length = 0;
                    }
                    i++;
                }
                else if (c == '[')
                {
                    if (sb.Length > 0)
                    {
                        segments.Add(new Segment { Name = sb.ToString() });
                        sb.Length = 0;
                    }

                    i++;
                    int start = i;
                    while (i < path.Length && path[i] != ']')
                        i++;

                    if (i >= path.Length || !int.TryParse(path.Substring(start, i - start), out var index))
                        return null;

                    segments.Add(new Segment { Index = index });
                    i++;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            if (sb.Length > 0)
                segments.Add(new Segment { Name = sb.ToString() });

            return segments;
        }

        private static object GetSegmentValue(object obj, Segment seg)
        {
            if (seg.Index.HasValue)
            {
                if (obj is not IList list)
                    return null;

                int idx = seg.Index.Value;
                return idx >= 0 && idx < list.Count ? list[idx] : null;
            }

            var t = Traverse.Create(obj);
            var prop = t.Property(seg.Name);
            if (prop.PropertyExists())
                return prop.GetValue();

            var field = t.Field(seg.Name);
            return field.FieldExists() ? field.GetValue() : null;
        }
    }
}