// graph_hash.c
// KMB 2005 Jul 14

#include "graph_hash.h"

static guint _graph_ONE=1;

struct _edge {
  node_t i,j; // 2 bytes each =>65536 nodes maximum
};

typedef struct _edge* edge_t;

struct _graph {
  node_t nnodes;
  GHashTable* hash;
  node_t *degree;        // degree of each node
  node_t *degree_dist;   // [k]=number of nodes of degree k
};

guint edge_hash(edge_t k) {
  node_t i,j;
  if (k->i<k->j) { i=k->i; j=k->j; }
  else           { i=k->j; j=k->i; }
  assert(i<j); // NB keys are sorted i<j
  gint x=(((gint)i)<<16)|j;
  //guint y=g_int_hash(&x); return y;
  return x; // identity hash
}

gboolean edge_equal(edge_t a, edge_t b) {
  return (a->i==b->i) && (a->j==b->j);
}

void edge_destroy(gpointer e) {
  free((edge_t)e);
}

graph_t graph_new(node_t nnodes) {
  if (nnodes==G_MAXUSHORT) {
    fprintf(stderr,"%hu too big in graph_new\n",nnodes);
    exit(1);
  }
  graph_t g=malloc(sizeof(graph_t));
  g->nnodes=nnodes;
  g->hash=g_hash_table_new_full((GHashFunc)edge_hash,(GEqualFunc)edge_equal,edge_destroy,NULL);
  g->degree=calloc(nnodes,sizeof(node_t));
  g->degree_dist=calloc(nnodes,sizeof(node_t));
  g->degree_dist[0]=nnodes;
  return g;
}

void graph_clear(graph_t g) {
  g_hash_table_destroy(g->hash);
  free(g);
}

void graph_set_edge(graph_t g, node_t i, node_t j) {
  if (graph_get_edge(g,i,j)) return; // FIXME ugly
  assert(i!=j);
  edge_t e=malloc(sizeof(struct _edge));
  e->i=i; e->j=j;
  g_hash_table_insert(g->hash,(gpointer)e,(gpointer)&_graph_ONE);
  node_t di=++g->degree[i],dj=++g->degree[j];
  g->degree_dist[di]++;
  g->degree_dist[dj]++;
  g->degree_dist[di-1]--;
  g->degree_dist[dj-1]--;
}

gboolean graph_get_edge(graph_t g, node_t i, node_t j) {
  gpointer v;
  struct _edge e;
  assert(i!=j);
  e.i=i; e.j=j;
  v=g_hash_table_lookup(g->hash,(gconstpointer)&e);
  if (v) return 1;
  return 0;
}

gboolean graph_del_edge(graph_t g, node_t i, node_t j) {
  struct _edge e;
  e.i=i; e.j=j;
  assert(i!=j);
  node_t di=--g->degree[i],dj=--g->degree[j];
  g->degree_dist[di]++;
  g->degree_dist[dj]++;
  g->degree_dist[di+1]--;
  g->degree_dist[dj+1]--;
  return g_hash_table_remove(g->hash,(gconstpointer)&e); // True if found
}

void graph_flip_edge(graph_t g, node_t i, node_t j) {
  if (i>j) { i^=j; j^=i; i^=j; } // xor swap
  struct _edge e={.i=i,.j=j};
  assert(i<j);
  node_t di=g->degree[i],dj=g->degree[j];
  g->degree_dist[di]--;
  g->degree_dist[dj]--;
  if (g_hash_table_lookup(g->hash,(gconstpointer)&e)) { // edge was present
    g_hash_table_remove(g->hash,(gconstpointer)&e);
    g->degree_dist[--g->degree[i]]++;
    g->degree_dist[--g->degree[j]]++;
  } else { // edge was absent
    edge_t e=malloc(sizeof(struct _edge));
    e->i=i; e->j=j;
    g_hash_table_insert(g->hash,(gpointer)e,(gpointer)&_graph_ONE);
    g->degree_dist[++g->degree[i]]++;
    g->degree_dist[++g->degree[j]]++;
  }
}

void graph_show_neighbours(graph_t g, node_t i) {
  struct _edge e={.i=i};
  node_t nn=0;
  void f(gpointer k, gpointer v, gpointer u) {
    if (i!=((edge_t)k)->i) return;
    e.j=((edge_t)k)->j;
    printf("%hu ",e.j);
    nn++;
  }
  g_hash_table_foreach(g->hash,f,NULL);
  printf(" total %hu neighbours\n",nn);
}

void graph_show(graph_t g) {
  void f(gpointer k, gpointer v, gpointer u) {
    struct _edge e;
    e.i=((edge_t)k)->i;
    e.j=((edge_t)k)->j;
    printf("%hu %hu  ",e.i,e.j);
  }
  g_hash_table_foreach(g->hash,f,NULL);
  printf("\n");
}

node_t graph_get_degree(graph_t g, node_t i) {
  return g->degree[i];
}

node_t graph_get_nedges(graph_t g) {
  return g_hash_table_size(g->hash);
}

void graph_show_degrees(graph_t g) {
  node_t i;
  for (i=0; i<g->nnodes; i++)
    if (g->degree[i]) printf("deg(%u)=%u\n",i,g->degree[i]);
}

void graph_show_degree_dist(graph_t g) {
  node_t i;
  for (i=0; i<g->nnodes; i++)
    if (g->degree_dist[i]) printf("degree %2u: %u nodes\n",i,g->degree_dist[i]);
}

void graph_write_dotfile(char* fn, graph_t g, uint n) {
  FILE *f;
  void x(gpointer k, gpointer v, gpointer u) {
    fprintf(f,"  %hu--%hu\n",((edge_t)k)->i,((edge_t)k)->j);
  }
  f=fopen(fn,"w");
  fprintf(f,"graph foo {\n");
  fprintf(f,"  /* automatically generated by graph_write_dotfile */\n");
  fprintf(f,"  graph [fontcolor=green,fontsize=8,center=true,ratio=1,size=8,8];\n  node  [shape=\"circle\",fixedsize=\"true\"] \n  node  [color=green,fontcolor=red,fontsize=8,height=0.4] \n  edge  [color=blue];\n");
  g_hash_table_foreach(g->hash,x,NULL);
  fprintf(f,"} /* end graph */\n");
  fclose(f);
  fprintf(stderr,"Now do: cat %s | neato -Tps | gv -\n",fn);
}

void graph_write_glossfile(char* fn, graph_t g, uint n) {
  FILE *f;
  void x(gpointer k, gpointer v, gpointer u) {
    fprintf(f,"%hu %hu  ",((edge_t)k)->i,((edge_t)k)->j);
  }
  f=fopen(fn,"w");
  g_hash_table_foreach(g->hash,x,NULL);
  fprintf(f,"\n");
  fclose(f);
  fprintf(stderr,"now do: cat %s | gloss | neato -Tps | gv -\n",fn);
}


void graph_show_glib_version(void) {
  fprintf(stderr,"glib version=%d.%d.%d\n",glib_major_version,glib_minor_version,glib_micro_version);
}