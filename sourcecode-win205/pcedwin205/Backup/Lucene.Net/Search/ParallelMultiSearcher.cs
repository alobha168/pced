/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Term = Lucene.Net.Index.Term;
using PriorityQueue = Lucene.Net.Util.PriorityQueue;

namespace Lucene.Net.Search
{
	
	/// <summary>Implements parallel search over a set of <code>Searchables</code>.
	/// 
	/// <p>Applications usually need only call the inherited {@link #Search(Query)}
	/// or {@link #Search(Query,Filter)} methods.
	/// </summary>
	public class ParallelMultiSearcher : MultiSearcher
	{
		private class AnonymousClassHitCollector1 : HitCollector
		{
			public AnonymousClassHitCollector1(Lucene.Net.Search.HitCollector results, int start, ParallelMultiSearcher enclosingInstance)
			{
				InitBlock(results, start, enclosingInstance);
			}
			private void  InitBlock(Lucene.Net.Search.HitCollector results, int start, ParallelMultiSearcher enclosingInstance)
			{
				this.results = results;
				this.start = start;
				this.enclosingInstance = enclosingInstance;
			}
			private Lucene.Net.Search.HitCollector results;
			private int start;
			private ParallelMultiSearcher enclosingInstance;
			public ParallelMultiSearcher Enclosing_Instance
			{
				get
				{
					return enclosingInstance;
				}
				
			}
			public override void  Collect(int doc, float score)
			{
				results.Collect(doc + start, score);
			}
		}
		
		private Lucene.Net.Search.Searchable[] searchables;
		private int[] starts;
		
		/// <summary>Creates a searcher which searches <i>searchables</i>. </summary>
		public ParallelMultiSearcher(Lucene.Net.Search.Searchable[] searchables) : base(searchables)
		{
			this.searchables = searchables;
			this.starts = GetStarts();
		}
		
		/// <summary> TODO: parallelize this one too</summary>
		public override int DocFreq(Term term)
		{
			return base.DocFreq(term);
		}
		
		/// <summary> A search implementation which spans a new thread for each
		/// Searchable, waits for each search to complete and merge
		/// the results back together.
		/// </summary>
		public override TopDocs Search(Weight weight, Filter filter, int nDocs)
		{
			HitQueue hq = new HitQueue(nDocs);
			int totalHits = 0;
			MultiSearcherThread[] msta = new MultiSearcherThread[searchables.Length];
			for (int i = 0; i < searchables.Length; i++)
			{
				// search each searcher
				// Assume not too many searchables and cost of creating a thread is by far inferior to a search
				msta[i] = new MultiSearcherThread(searchables[i], weight, filter, nDocs, hq, i, starts, "MultiSearcher thread #" + (i + 1));
				msta[i].Start();
			}
			
			for (int i = 0; i < searchables.Length; i++)
			{
				try
				{
					msta[i].Join();
				}
				catch (System.Threading.ThreadInterruptedException)
				{
					; // TODO: what should we do with this???
				}
				System.IO.IOException ioe = msta[i].GetIOException();
				if (ioe == null)
				{
					totalHits += msta[i].Hits();
				}
				else
				{
					// if one search produced an IOException, rethrow it
					throw ioe;
				}
			}
			
			ScoreDoc[] scoreDocs = new ScoreDoc[hq.Size()];
			for (int i = hq.Size() - 1; i >= 0; i--)
			// put docs in array
				scoreDocs[i] = (ScoreDoc) hq.Pop();
			
			float maxScore = (totalHits == 0) ? System.Single.NegativeInfinity : scoreDocs[0].score;
			
			return new TopDocs(totalHits, scoreDocs, maxScore);
		}
		
		/// <summary> A search implementation allowing sorting which spans a new thread for each
		/// Searchable, waits for each search to complete and merges
		/// the results back together.
		/// </summary>
		public override TopFieldDocs Search(Weight weight, Filter filter, int nDocs, Sort sort)
		{
			// don't specify the fields - we'll wait to do this until we get results
			FieldDocSortedHitQueue hq = new FieldDocSortedHitQueue(null, nDocs);
			int totalHits = 0;
			MultiSearcherThread[] msta = new MultiSearcherThread[searchables.Length];
			for (int i = 0; i < searchables.Length; i++)
			{
				// search each searcher
				// Assume not too many searchables and cost of creating a thread is by far inferior to a search
				msta[i] = new MultiSearcherThread(searchables[i], weight, filter, nDocs, hq, sort, i, starts, "MultiSearcher thread #" + (i + 1));
				msta[i].Start();
			}
			
			float maxScore = System.Single.NegativeInfinity;
			
			for (int i = 0; i < searchables.Length; i++)
			{
				try
				{
					msta[i].Join();
				}
				catch (System.Threading.ThreadInterruptedException)
				{
					; // TODO: what should we do with this???
				}
				System.IO.IOException ioe = msta[i].GetIOException();
				if (ioe == null)
				{
					totalHits += msta[i].Hits();
					maxScore = System.Math.Max(maxScore, msta[i].GetMaxScore());
				}
				else
				{
					// if one search produced an IOException, rethrow it
					throw ioe;
				}
			}
			
			ScoreDoc[] scoreDocs = new ScoreDoc[hq.Size()];
			for (int i = hq.Size() - 1; i >= 0; i--)
			// put docs in array
				scoreDocs[i] = (ScoreDoc) hq.Pop();
			
			return new TopFieldDocs(totalHits, scoreDocs, hq.GetFields(), maxScore);
		}
		
		/// <summary>Lower-level search API.
		/// 
		/// <p>{@link HitCollector#Collect(int,float)} is called for every non-zero
		/// scoring document.
		/// 
		/// <p>Applications should only use this if they need <i>all</i> of the
		/// matching documents.  The high-level search API ({@link
		/// Searcher#Search(Query)}) is usually more efficient, as it skips
		/// non-high-scoring hits.
		/// 
		/// </summary>
		/// <param name="weight">to match documents
		/// </param>
		/// <param name="filter">if non-null, a bitset used to eliminate some documents
		/// </param>
		/// <param name="results">to receive hits
		/// 
		/// </param>
		/// <todo>  parallelize this one too </todo>
		public override void  Search(Weight weight, Filter filter, HitCollector results)
		{
			for (int i = 0; i < searchables.Length; i++)
			{
				
				int start = starts[i];
				
				searchables[i].Search(weight, filter, new AnonymousClassHitCollector1(results, start, this));
			}
		}
		
		/*
		* TODO: this one could be parallelized too
		* @see Lucene.Net.search.Searchable#rewrite(Lucene.Net.search.Query)
		*/
		public override Query Rewrite(Query original)
		{
			return base.Rewrite(original);
		}
	}
	
	/// <summary> A thread subclass for searching a single searchable </summary>
	class MultiSearcherThread : SupportClass.ThreadClass
	{
		
		private Lucene.Net.Search.Searchable searchable;
		private Weight weight;
		private Filter filter;
		private int nDocs;
		private TopDocs docs;
		private int i;
		private PriorityQueue hq;
		private int[] starts;
		private System.IO.IOException ioe;
		private Sort sort;
		
		public MultiSearcherThread(Lucene.Net.Search.Searchable searchable, Weight weight, Filter filter, int nDocs, HitQueue hq, int i, int[] starts, System.String name):base(name)
		{
			this.searchable = searchable;
			this.weight = weight;
			this.filter = filter;
			this.nDocs = nDocs;
			this.hq = hq;
			this.i = i;
			this.starts = starts;
		}
		
		public MultiSearcherThread(Lucene.Net.Search.Searchable searchable, Weight weight, Filter filter, int nDocs, FieldDocSortedHitQueue hq, Sort sort, int i, int[] starts, System.String name):base(name)
		{
			this.searchable = searchable;
			this.weight = weight;
			this.filter = filter;
			this.nDocs = nDocs;
			this.hq = hq;
			this.i = i;
			this.starts = starts;
			this.sort = sort;
		}
		
		override public void  Run()
		{
			try
			{
				docs = (sort == null) ? searchable.Search(weight, filter, nDocs) : searchable.Search(weight, filter, nDocs, sort);
			}
			// Store the IOException for later use by the caller of this thread
			catch (System.IO.IOException ioe)
			{
				this.ioe = ioe;
			}
			if (this.ioe == null)
			{
				// if we are sorting by fields, we need to tell the field sorted hit queue
				// the actual type of fields, in case the original list contained AUTO.
				// if the searchable returns null for fields, we'll have problems.
				if (sort != null)
				{
					((FieldDocSortedHitQueue) hq).SetFields(((TopFieldDocs) docs).fields);
				}
				ScoreDoc[] scoreDocs = docs.scoreDocs;
				for (int j = 0; j < scoreDocs.Length; j++)
				{
					// merge scoreDocs into hq
					ScoreDoc scoreDoc = scoreDocs[j];
					scoreDoc.doc += starts[i]; // convert doc 
					//it would be so nice if we had a thread-safe insert 
					lock (hq)
					{
						if (!hq.Insert(scoreDoc))
							break;
					} // no more scores > minScore
				}
			}
		}
		
		public virtual int Hits()
		{
			return docs.totalHits;
		}
		
		public virtual float GetMaxScore()
		{
			return docs.GetMaxScore();
		}
		
		public virtual System.IO.IOException GetIOException()
		{
			return ioe;
		}
	}
}