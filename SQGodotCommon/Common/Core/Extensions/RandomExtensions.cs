using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Core;

public static class RandomExtensions
{
	/// <summary>
	/// Shuffles a list in place (modifies the existing list)
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="deck"></param>
	public static void Shuffle<T>(this List<T> deck)
	{
		Random random = new Random();
		int n = deck.Count;
		while (n > 1)
		{
			n--;
			int k = random.Next(n + 1);
			T value = deck[k];
			deck[k] = deck[n];
			deck[n] = value;
		}
	}

	/// <summary>
	/// Randomizes a list without modifying the original list
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="list"></param>
	/// <returns></returns>
	public static List<T> Randomize<T>(this List<T> list)
	{
		var newList = list.ToList();
		newList.Shuffle();
		return newList;
	}
}
