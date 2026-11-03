using System.Text;

class GameReport
{
    public string gamename, result;

    public GameReport(string gamename, PolicyResult[] flat, PolicyResult[] bestPolicies, long elapsedms, int numSeeds, int numSteps, int numRollouts, long totalFrames, string[] storageNames, List<Template> templates)
    {
        List<PolicyResult>[] bags = new List<PolicyResult>[bestPolicies.Length];
        for (int i = 0; i < bags.Length; i++) bags[i] = [];
        for (int i = 0; i < flat.Length; i++)
        {
            PolicyResult e = flat[i];
            bags[e.length - 1].Add(e);
        }

        this.gamename = gamename;
        StringBuilder sb = new();

        sb.AppendLine($"<game name=\"{gamename}\" maxlength=\"{bags.Length}\" time=\"{elapsedms / 1000f:0.#}\" timePerSimulation=\"{(float)elapsedms / numRollouts:0.00}\" steps=\"{numSteps}\" numSeeds=\"{numSeeds}\" numPolicies=\"{flat.Length}\" numRollouts=\"{numRollouts}\" rolloutsPerPolicy=\"{(float)numRollouts / flat.Length:0.00}\" averageFrames=\"{(numRollouts == 0 ? 0 : (double)totalFrames / numRollouts):0.##}\">");
        sb.AppendLine("  <histogram>");

        for (int l = 0; l < bags.Length; l++)
        {
            List<PolicyResult> bag = bags[l];
            sb.Append($"    <item length=\"{l + 1}\" numPolicies=\"{bag.Count}\"");
            if (bag.Count > 0)
            {
                PolicyResult best = bestPolicies[l];
                sb.Append($" bestScore=\"{best.minScore:0.##}\" bestPolicy=\"{best.ToString(storageNames, templates)}\" scores=\"{best.Join()}\"");
            }
            sb.AppendLine("/>");
        }
        sb.AppendLine("  </histogram>");

        sb.AppendLine("  <history>");
        float bestAverage = -1e+6f;
        for (int i = 0; i < flat.Length; i++)
        {
            PolicyResult e = flat[i];
            if (e.minScore > bestAverage)
            {
                string join = "";
                foreach (float f in e.scores) join += $"{f:0.##} ";
                join = join[..^1];

                sb.AppendLine($"    <policy n=\"{i}\" minscore=\"{e.minScore:0.##}\" text=\"{e.ToString(storageNames, templates)}\" length=\"{e.length}\" scores=\"{join}\"/>");
                bestAverage = e.minScore;
            }
        }
        sb.AppendLine("  </history>");

        float totalScore = flat.Sum(e => e.minScore);
        /*Dictionary<string, (float sum, int count)> subexpressions = [];
        HashSet<string> seen = [];
        for (int i = 0; i < flat.Length; i++)
        {
            seen.Clear();
            CollectSubexpressions(flat[i].expression, flat[i].root, seen, storageNames, templates);
            foreach (string sub in seen)
            {
                subexpressions.TryGetValue(sub, out (float sum, int count) stat);
                subexpressions[sub] = (stat.sum + flat[i].minScore, stat.count + 1);
            }
        }
        sb.AppendLine("  <subexpressions>");
        foreach ((string sub, (float sum, int count) stat) in subexpressions.OrderByDescending(pair => pair.Value.sum / pair.Value.count))
        {
            int numWithout = flat.Length - stat.count;
            float averageWithout = numWithout == 0 ? float.NaN : (totalScore - stat.sum) / numWithout;
            sb.AppendLine($"    <subexpression text=\"{sub}\" averageScoreWith=\"{stat.sum / stat.count:0.##}\" averageScoreWithout=\"{averageWithout:0.##}\" numPoliciesWith=\"{stat.count}\" numPoliciesWithout=\"{numWithout}\"/>");
        }
        sb.AppendLine("  </subexpressions>");*/

        if (Settings.SAVE_ALL_POLICIES)
        {
            sb.AppendLine("  <training>");
            for (int i = 0; i < flat.Length; i++)
            {
                PolicyResult e = flat[i];
                if (e.length < bestPolicies.Length) sb.AppendLine($"    <policy score=\"{e.minScore}\" text=\"{e.ToString(null, templates)}\"/>");
            }
            sb.AppendLine("  </training>");
            sb.AppendLine("  <validation>");
            for (int i = 0; i < flat.Length; i++)
            {
                PolicyResult e = flat[i];
                if (e.length == bestPolicies.Length) sb.AppendLine($"    <policy score=\"{e.minScore}\" text=\"{e.ToString(null, templates)}\"/>");
            }
            sb.AppendLine("  </validation>");
        }

        sb.AppendLine("</game>");
        result = sb.ToString();
    }

    /*static void CollectSubexpressions(Expression expression, int id, HashSet<string> result, string[] storageNames, List<Template> templates)
    {
        Node node = expression.nodes[id];
        if (node.length <= Settings.MAX_SUB_LENGTH) result.Add(expression.ToString(id, storageNames, templates));
        for (int i = 0; i < node.ch.Length; i++) CollectSubexpressions(expression, node.ch[i], result, storageNames, templates);
    }*/
    }

    class Report
{
    List<GameReport> reports;

    public Report()
    {
        reports = [];
    }

    public void Add(GameReport report)
    {
        reports.Add(report);
    }

    public void Save()
    {
        if (reports.Count == 0) return;

        string result = "";
        if (reports.Count > 1) result += "<games>" + Environment.NewLine;
        foreach (GameReport report in reports) result += report.result;
        if (reports.Count > 1) result += "</games>" + Environment.NewLine;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
        string filename = $"{timestamp}-{reports[0].gamename}-{reports[^1].gamename}";
        if (reports.Count == 1) filename = $"{timestamp}-{reports[0].gamename}";
        File.WriteAllText($"reports/{filename}.xml", result);
    }
}
