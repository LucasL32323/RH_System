-- HR-Auto | Modelo físico (SQLite) — 25 tabelas
-- Mapeamento de tipos: INT -> INTEGER | VARCHAR/CHAR -> TEXT | DATE -> TEXT (ISO 8601, AAAA-MM-DD) | FLOAT -> REAL
-- Campos sim/não (ativo, lida, obrigatorio...) usam INTEGER 0/1. Horários do ponto: TEXT 'HH:MM:SS'.
PRAGMA foreign_keys = ON;

CREATE TABLE PERFIL (
  id_perfil INTEGER PRIMARY KEY AUTOINCREMENT,
  nome TEXT,
  descricao TEXT
);

CREATE TABLE CARGO (
  id_cargo INTEGER PRIMARY KEY AUTOINCREMENT,
  nome_cargo TEXT,
  descricao TEXT
);

CREATE TABLE DEPARTAMENTO (
  id_departamento INTEGER PRIMARY KEY AUTOINCREMENT,
  nome TEXT,
  descricao TEXT
);

CREATE TABLE FUNCIONARIO (
  id_funcionario INTEGER PRIMARY KEY AUTOINCREMENT,
  nome TEXT,
  data_nascimento TEXT,
  data_admissao TEXT,
  data_desligamento TEXT,
  ativo INTEGER,
  email TEXT,
  telefone TEXT,
  id_cargo INTEGER NOT NULL,
  id_departamento INTEGER NOT NULL,
  id_gestor INTEGER,
  FOREIGN KEY (id_cargo) REFERENCES CARGO(id_cargo),
  FOREIGN KEY (id_departamento) REFERENCES DEPARTAMENTO(id_departamento),
  FOREIGN KEY (id_gestor) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE USUARIO (
  id_usuario INTEGER PRIMARY KEY AUTOINCREMENT,
  id_funcionario INTEGER NOT NULL,
  id_perfil INTEGER NOT NULL,
  email TEXT,
  senha TEXT,
  ativo INTEGER,
  ultimo_login TEXT,
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario),
  FOREIGN KEY (id_perfil) REFERENCES PERFIL(id_perfil)
);

CREATE TABLE LOG_AUDITORIA (
  id_log INTEGER PRIMARY KEY AUTOINCREMENT,
  id_usuario INTEGER NOT NULL,
  data_hora TEXT,
  acao TEXT,
  tabela TEXT,
  id_registro INTEGER,
  descricao TEXT,
  FOREIGN KEY (id_usuario) REFERENCES USUARIO(id_usuario)
);

CREATE TABLE FERIADO (
  id_feriado INTEGER PRIMARY KEY AUTOINCREMENT,
  data TEXT,
  nome TEXT,
  tipo TEXT,
  cidade TEXT,
  estado TEXT
);

CREATE TABLE FUNCIONARIO_DETALHES (
  id_funcionario INTEGER NOT NULL,
  cpf TEXT,
  rg TEXT,
  pis TEXT,
  ctps TEXT,
  endereco TEXT,
  numero TEXT,
  complemento TEXT,
  bairro TEXT,
  cidade TEXT,
  estado TEXT,
  cep TEXT,
  PRIMARY KEY (id_funcionario),
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE FERIAS (
  id_ferias INTEGER PRIMARY KEY AUTOINCREMENT,
  id_funcionario INTEGER NOT NULL,
  data_inicio TEXT,
  data_fim TEXT,
  dias INTEGER,
  status TEXT,
  data_solicitacao TEXT,
  observacao TEXT,
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE HOLERITE (
  id_holerite INTEGER PRIMARY KEY AUTOINCREMENT,
  id_funcionario INTEGER NOT NULL,
  mes INTEGER,
  ano INTEGER,
  data_publicacao TEXT,
  arquivo TEXT,
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE NOTIFICACAO (
  id_notificacao INTEGER PRIMARY KEY AUTOINCREMENT,
  id_funcionario INTEGER NOT NULL,
  tipo TEXT,
  titulo TEXT,
  mensagem TEXT,
  data_envio TEXT,
  lida INTEGER,
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE BENEFICIO (
  id_beneficio INTEGER PRIMARY KEY AUTOINCREMENT,
  nome TEXT,
  descricao TEXT,
  ativo INTEGER
);

CREATE TABLE FUNCIONARIO_BENEFICIO (
  id_funcionario INTEGER NOT NULL,
  id_beneficio INTEGER NOT NULL,
  data_adesao TEXT,
  PRIMARY KEY (id_funcionario, id_beneficio),
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario),
  FOREIGN KEY (id_beneficio) REFERENCES BENEFICIO(id_beneficio)
);

CREATE TABLE PONTO (
  id_ponto INTEGER PRIMARY KEY AUTOINCREMENT,
  id_funcionario INTEGER NOT NULL,
  data TEXT,
  entrada TEXT,
  saida_intervalo TEXT,
  retorno_intervalo TEXT,
  saida TEXT,
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE MANUTENCAO_PONTO (
  id_manutencao INTEGER PRIMARY KEY AUTOINCREMENT,
  id_ponto INTEGER NOT NULL,
  id_funcionario INTEGER NOT NULL,
  justificativa TEXT,
  data_solicitacao TEXT,
  status TEXT,
  data_analise TEXT,
  id_responsavel INTEGER,
  observacao TEXT,
  FOREIGN KEY (id_ponto) REFERENCES PONTO(id_ponto),
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario),
  FOREIGN KEY (id_responsavel) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE BANCO_HORAS (
  id_banco_horas INTEGER PRIMARY KEY AUTOINCREMENT,
  id_funcionario INTEGER NOT NULL,
  data TEXT,
  tipo TEXT,
  quantidade REAL,
  origem TEXT,
  observacao TEXT,
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE ADVERTENCIA (
  id_advertencia INTEGER PRIMARY KEY AUTOINCREMENT,
  id_funcionario INTEGER NOT NULL,
  id_responsavel INTEGER NOT NULL,
  data TEXT,
  tipo TEXT,
  motivo TEXT,
  descricao TEXT,
  status TEXT,
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario),
  FOREIGN KEY (id_responsavel) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE AVISO (
  id_aviso INTEGER PRIMARY KEY AUTOINCREMENT,
  id_autor INTEGER NOT NULL,
  titulo TEXT,
  conteudo TEXT,
  data_publicacao TEXT,
  data_expiracao TEXT,
  tipo TEXT,
  FOREIGN KEY (id_autor) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE AVISO_FUNCIONARIO (
  id_aviso INTEGER NOT NULL,
  id_funcionario INTEGER NOT NULL,
  visualizado INTEGER,
  data_visualizacao TEXT,
  PRIMARY KEY (id_aviso, id_funcionario),
  FOREIGN KEY (id_aviso) REFERENCES AVISO(id_aviso),
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE TREINAMENTO (
  id_treinamento INTEGER PRIMARY KEY AUTOINCREMENT,
  titulo TEXT,
  descricao TEXT,
  tipo TEXT,
  data_inicio TEXT,
  data_fim TEXT,
  obrigatorio INTEGER
);

CREATE TABLE TREINAMENTO_FUNCIONARIO (
  id_treinamento INTEGER NOT NULL,
  id_funcionario INTEGER NOT NULL,
  status TEXT,
  data_inicio TEXT,
  data_conclusao TEXT,
  nota REAL,
  PRIMARY KEY (id_treinamento, id_funcionario),
  FOREIGN KEY (id_treinamento) REFERENCES TREINAMENTO(id_treinamento),
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE CERTIFICADO (
  id_certificado INTEGER PRIMARY KEY AUTOINCREMENT,
  id_treinamento INTEGER NOT NULL,
  id_funcionario INTEGER NOT NULL,
  data_emissao TEXT,
  codigo_validacao TEXT,
  FOREIGN KEY (id_treinamento) REFERENCES TREINAMENTO(id_treinamento),
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE FORMULARIO (
  id_formulario INTEGER PRIMARY KEY AUTOINCREMENT,
  titulo TEXT,
  descricao TEXT,
  tipo TEXT,
  id_autor INTEGER NOT NULL,
  data_criacao TEXT,
  ativo INTEGER,
  FOREIGN KEY (id_autor) REFERENCES FUNCIONARIO(id_funcionario)
);

CREATE TABLE PERGUNTA (
  id_pergunta INTEGER PRIMARY KEY AUTOINCREMENT,
  id_formulario INTEGER NOT NULL,
  pergunta TEXT,
  tipo_resposta TEXT,
  ordem INTEGER,
  obrigatoria INTEGER,
  FOREIGN KEY (id_formulario) REFERENCES FORMULARIO(id_formulario)
);

CREATE TABLE RESPOSTA (
  id_resposta INTEGER PRIMARY KEY AUTOINCREMENT,
  id_pergunta INTEGER NOT NULL,
  id_funcionario INTEGER,
  resposta TEXT,
  data_resposta TEXT,
  FOREIGN KEY (id_pergunta) REFERENCES PERGUNTA(id_pergunta),
  FOREIGN KEY (id_funcionario) REFERENCES FUNCIONARIO(id_funcionario)
);

-- Índices nas chaves estrangeiras mais consultadas
CREATE INDEX idx_ponto_func_data ON PONTO(id_funcionario, data);
CREATE INDEX idx_ferias_func ON FERIAS(id_funcionario);
CREATE INDEX idx_holerite_func ON HOLERITE(id_funcionario, ano, mes);
CREATE INDEX idx_notificacao_func ON NOTIFICACAO(id_funcionario, lida);
CREATE INDEX idx_log_usuario ON LOG_AUDITORIA(id_usuario, data_hora);
