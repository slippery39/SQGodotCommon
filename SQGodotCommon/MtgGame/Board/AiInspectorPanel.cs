using System.Collections.Generic;
using System.Linq;
using MtgSimulator;

namespace MtgGame;

/// <summary>
/// Shows what the AI was thinking on its last decision: every action it ranked, and the evaluator
/// score of the chosen one broken into the terms that produced it.
///
/// The point of this panel is the TERM BREAKDOWN, not the score. "This action scores 4.52" is not
/// a diagnosis; "creatures +3.00, power +4.00, race -3.20, hand -1.40, lands-in-hand +0.00" is —
/// the land-pricing defect is visible on sight in the second form and invisible in the first. Two
/// sessions were spent arguing evaluation changes that could have been settled by looking at this.
///
/// Structured like <see cref="EventLogPanel"/> deliberately: own CanvasLayer, closed by default,
/// same anchors, so the two overlays behave identically and neither drives MainColumn's height.
/// </summary>
public partial class AiInspectorPanel : CanvasLayer
{
	private PanelContainer _panel = null!;
	private Label _headline = null!;
	private VBoxContainer _candidateList = null!;
	private VBoxContainer _breakdownHost = null!;

	private AiDecision? _decision;
	private int _selectedIndex;

	public bool IsOpen { get; private set; }

	public override void _Ready()
	{
		// Layer 2 so it draws above the event log rather than fighting it for the same strip.
		Layer = 2;

		_panel = new PanelContainer();
		_panel.AnchorLeft = 0.60f;
		_panel.AnchorRight = 1.0f;
		_panel.AnchorTop = 0.0f;
		_panel.AnchorBottom = 1.0f;
		_panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(1, MtgUiStyles.GoldBorder));
		AddChild(_panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		_panel.AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 8);
		margin.AddChild(root);

		var title = new Label { Text = "AI Inspector  (F6)" };
		title.AddThemeFontSizeOverride("font_size", 24);
		root.AddChild(title);

		_headline = new Label { Text = "No decision captured yet." };
		_headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_headline.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(_headline);

		root.AddChild(new HSeparator());

		var candidateHeader = new Label { Text = "Actions considered" };
		candidateHeader.AddThemeFontSizeOverride("font_size", 16);
		candidateHeader.Modulate = new Color(0.70f, 0.75f, 0.85f, 1f);
		root.AddChild(candidateHeader);

		var candidateScroll = new ScrollContainer();
		candidateScroll.CustomMinimumSize = new Vector2(0, 220);
		candidateScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		root.AddChild(candidateScroll);

		_candidateList = new VBoxContainer();
		_candidateList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_candidateList.AddThemeConstantOverride("separation", 2);
		candidateScroll.AddChild(_candidateList);

		root.AddChild(new HSeparator());

		var breakdownScroll = new ScrollContainer();
		breakdownScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		breakdownScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		root.AddChild(breakdownScroll);

		_breakdownHost = new VBoxContainer();
		_breakdownHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_breakdownHost.AddThemeConstantOverride("separation", 4);
		breakdownScroll.AddChild(_breakdownHost);

		SetOpen(false);
	}

	public void SetOpen(bool open)
	{
		IsOpen = open;
		_panel.Visible = open;
	}

	public void Toggle() => SetOpen(!IsOpen);

	/// <summary>
	/// Called after every AI step. Rebuilding only when the decision object actually changed keeps
	/// this off the hot path of a fast AI turn — the scene calls it far more often than the AI
	/// produces new decisions.
	/// </summary>
	public void Show(AiDecision? decision)
	{
		if (ReferenceEquals(decision, _decision))
			return;

		_decision = decision;
		_selectedIndex = decision?.Candidates.ToList().FindIndex(c => c.WasChosen) ?? -1;
		if (_selectedIndex < 0)
			_selectedIndex = 0;

		Rebuild();
	}

	private void Rebuild()
	{
		ClearChildren(_candidateList);
		ClearChildren(_breakdownHost);

		if (_decision == null)
		{
			_headline.Text = "No decision captured yet.";
			return;
		}

		_headline.Text =
			$"{(_decision.IsChoiceResolution ? "Resolved choice" : "Chose")}: {_decision.Description}\n"
			+ $"score {_decision.Score:F2}";

		var candidates = _decision.Candidates;
		for (var i = 0; i < candidates.Count; i++)
		{
			var index = i;
			var c = candidates[i];
			var button = new Button
			{
				Text = $"{c.Score, 9:F2}   {c.Description}" + (c.WasChosen ? "   <" : ""),
				Alignment = HorizontalAlignment.Left,
				ToggleMode = true,
				ButtonPressed = i == _selectedIndex,
			};
			button.AddThemeFontSizeOverride("font_size", 16);
			if (c.WasChosen)
				button.Modulate = new Color(1f, 0.90f, 0.55f, 1f);
			button.Pressed += () =>
			{
				_selectedIndex = index;
				Rebuild();
			};
			_candidateList.AddChild(button);
		}

		BuildBreakdown();
	}

	/// <summary>
	/// The diagnostic view: every term of the evaluator, before the action and after it, with the
	/// delta. The delta column is the one that matters — it says what the action actually bought,
	/// and a resource priced at zero shows up as a stubborn 0.00 next to a cost that plainly is not
	/// free.
	/// </summary>
	private void BuildBreakdown()
	{
		if (_decision == null || _selectedIndex >= _decision.Candidates.Count)
			return;

		var candidate = _decision.Candidates[_selectedIndex];
		var before = _decision.StateBefore;
		var after = candidate.Breakdown;

		var header = new Label { Text = $"Breakdown — {candidate.Description}" };
		header.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		header.AddThemeFontSizeOverride("font_size", 17);
		header.Modulate = new Color(0.70f, 0.75f, 0.85f, 1f);
		_breakdownHost.AddChild(header);

		if (after == null)
		{
			var missing = new Label
			{
				Text =
					"No breakdown captured for this action.\n"
					+ "(It threw while being replayed for display, which does not affect the game.)",
			};
			missing.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			missing.AddThemeFontSizeOverride("font_size", 15);
			_breakdownHost.AddChild(missing);
			return;
		}

		var grid = new GridContainer { Columns = 4 };
		grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		grid.AddThemeConstantOverride("h_separation", 14);
		grid.AddThemeConstantOverride("v_separation", 3);
		_breakdownHost.AddChild(grid);

		AddRow(grid, "term", "before", "after", "delta", isHeader: true);

		var afterTerms = after.Value.Terms.ToList();
		var beforeTerms = before?.Terms.ToList();

		for (var i = 0; i < afterTerms.Count; i++)
		{
			var (label, afterValue) = afterTerms[i];
			// Terminal breakdowns carry a single row rather than the eight terms, so the two sides
			// only line up when neither is terminal. Fall back to showing the after value alone.
			var beforeValue =
				beforeTerms != null && beforeTerms.Count == afterTerms.Count
					? beforeTerms[i].Value
					: (float?)null;

			AddRow(
				grid,
				label,
				beforeValue?.ToString("F2") ?? "-",
				afterValue.ToString("F2"),
				beforeValue.HasValue ? Signed(afterValue - beforeValue.Value) : "-",
				deltaValue: beforeValue.HasValue ? afterValue - beforeValue.Value : 0f
			);
		}

		_breakdownHost.AddChild(new HSeparator());

		var totalGrid = new GridContainer { Columns = 4 };
		totalGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		totalGrid.AddThemeConstantOverride("h_separation", 14);
		_breakdownHost.AddChild(totalGrid);

		AddRow(
			totalGrid,
			"total",
			before?.Total.ToString("F2") ?? "-",
			after.Value.Total.ToString("F2"),
			before.HasValue ? Signed(after.Value.Total - before.Value.Total) : "-",
			deltaValue: before.HasValue ? after.Value.Total - before.Value.Total : 0f,
			isHeader: true
		);

		// The ranked score is a ROLLOUT score — the evaluation of a state two turns ahead — not the
		// total above. They will not agree, and the gap is exactly what the lookahead contributed.
		var note = new Label
		{
			Text =
				$"ranked on rollout score {candidate.Score:F2}\n"
				+ "(evaluation two turns ahead; the total above is the immediate position)",
		};
		note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		note.AddThemeFontSizeOverride("font_size", 14);
		note.Modulate = new Color(0.65f, 0.68f, 0.75f, 1f);
		_breakdownHost.AddChild(note);
	}

	private static string Signed(float v) => v >= 0 ? $"+{v:F2}" : v.ToString("F2");

	private static void AddRow(
		GridContainer grid,
		string term,
		string before,
		string after,
		string delta,
		float deltaValue = 0f,
		bool isHeader = false
	)
	{
		var fontSize = isHeader ? 16 : 15;
		var dim = new Color(0.65f, 0.68f, 0.75f, 1f);

		var termLabel = new Label { Text = term };
		termLabel.AddThemeFontSizeOverride("font_size", fontSize);
		termLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (isHeader)
			termLabel.Modulate = dim;
		grid.AddChild(termLabel);

		foreach (var (text, isDelta) in new[] { (before, false), (after, false), (delta, true) })
		{
			var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Right };
			label.AddThemeFontSizeOverride("font_size", fontSize);
			label.CustomMinimumSize = new Vector2(74, 0);
			if (isHeader)
				label.Modulate = dim;
			else if (isDelta && deltaValue > 0.005f)
				label.Modulate = new Color(0.55f, 0.90f, 0.55f, 1f);
			else if (isDelta && deltaValue < -0.005f)
				label.Modulate = new Color(0.95f, 0.55f, 0.55f, 1f);
			grid.AddChild(label);
		}
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
