# Étape 1 Mac — Mainframe + section BRAIN + Trousseau

Date : 2026-10-29. Fait partie de la feuille de route `docs/mac-parity-todo.md` (parité Mac ↔ Windows).

## But

Sur Mac (`cross/`), Capri gagne une fenêtre **Capri Mainframe** avec la section BRAIN de Windows :
trois paliers de cerveau (local, NVIDIA gratuit, clé payante), connexion/oubli d'une clé API, choix du
modèle. Les clés API sont rangées dans le **Trousseau macOS**, jamais en clair.

Hors périmètre : le moteur llama.cpp intégré et le téléchargement automatique d'un modèle (étape 2),
les sections VOICE/CAPRI du Mainframe (étapes 3 et 5), l'usage simultané de deux cerveaux (étape 8).

## Composants

### 1. `cross/Keys.cs` (nouveau)

- `Keys.Save(string name, string key)`, `Keys.Read(string name)` → `string` ou `null`,
  `Keys.Forget(string name)`.
- Noms utilisés : `nvidia`, `paid`, `voice` (clé ElevenLabs).
- **Mac** : Trousseau via Security.framework en P/Invoke (`SecItemAdd`, `SecItemCopyMatching`,
  `SecItemUpdate`, `SecItemDelete`, mots de passe génériques). Service `capri` ; en mode `--smoke`,
  service `capri-smoke`. Pas d'appel à la commande `security`.
- **Linux** : un fichier `<nom>-key` par clé dans le dossier de Capri, permissions `600` (la clé voix
  reste donc dans `voice-key`, comme avant).
- `Keys.Save` signale l'échec (exception) ; `Keys.Read` renvoie `null` si l'accès est refusé et
  expose la raison pour que l'appelant affiche un message.
- `Keys.Broken` (test uniquement) fait échouer `Save`, pour tester la migration ratée.
- **Migration** (Mac seulement) au démarrage : si l'ancien fichier `voice-key` (clé ElevenLabs en clair, cf.
  `Pet.KeyFile`) existe, il est copié dans le Trousseau sous `voice`, puis supprimé **uniquement** si
  la copie a réussi. Le code voix existant lit/écrit désormais via `Keys` au lieu de `KeyFile`.

### 2. `cross/Mainframe.cs` (nouveau)

Fenêtre Avalonia au look de Windows (`app/Mainframe.cs`) : fond `Look.Panel`, titre `MAINFRAME` en
mono, bordures rouges. Une seule section : **BRAIN**.

- Ligne d'état : cerveau actif (ex. « Ollama · gemma4:e2b » / « Running on this Mac », ou
  « NVIDIA · <modèle> » / « Using your NVIDIA key »).
- Trois paliers (boutons radio) :
  - **NORMAL** (palier 0) : modèles Ollama installés, menu + bouton Refresh. Si Ollama est absent ou
    sans modèle : note « Install Ollama, then run: ollama pull gemma4:e2b » et bouton vers
    https://ollama.com/download.
  - **HIGHER** (palier 1) : clé NVIDIA. Champ masqué + Connect/Forget, bouton **Free key** qui ouvre
    https://build.nvidia.com/settings/api-keys, note « Most NVIDIA models can't see your screen. ».
  - **MAX** (palier 2) : clé OpenAI, Claude ou DeepSeek. Champ masqué + Connect/Forget.
- En HIGHER/MAX connectés : menu des modèles cloud du fournisseur + Refresh.
- Une note d'erreur/progrès sous les paliers (« Checking key... », messages d'erreur).
- Une seule instance de la fenêtre ; la rouvrir la ramène au premier plan.
- Expose pour les tests : `TiersFit` (rien ne déborde) et `TextStatus` (comme Windows).

### 3. `cross/Pet.cs` (modifié)

- Portage depuis `app/Capri.cs` (~l.3692-3830) : `ChooseTier`, `ConnectText`, `ForgetText`,
  `ListCloud`, `TierCloud`, `Keep`, `ShowTiers`. Les accès `preferences.NvidiaKey/PaidKey`
  deviennent `Keys.Read/Save/Forget("nvidia"|"paid")`.
- `Brain.Remote`, `Cloud`, `Brain.CloudModels`, `Brain.DefaultCloudModel` sont réutilisés tels quels
  depuis `app/Brain.cs` (code partagé).
- `Settings` (`cross.json`) gagne `Tier` (int), `NvidiaModel`, `PaidModel` (string). Le modèle local
  reste dans `Model`. **Aucune clé n'est jamais écrite dans `cross.json`.**
- Le palier NORMAL réutilise `RefreshBrains` / `PickBrain` / `ShowBrains` existants.
- Message « cerveau hors ligne » (`Pet.Offline`) : « My brain's offline. Install Ollama from
  ollama.com, then run: ollama pull gemma4:e2b, or open Capri Mainframe for a free NVIDIA key. »

### 4. Menu clic droit

- Nouvelle entrée **Capri Mainframe** qui ouvre la fenêtre.
- Le sous-menu Brain liste les modèles du palier actif : Ollama en NORMAL, modèles cloud en
  HIGHER/MAX.

## Comportement

Logique identique à Windows sauf mention 🍎.

**Connect.** `Cloud.KindOf(key)` identifie le fournisseur ; inconnu → « That isn't an NVIDIA,
OpenAI, Claude or DeepSeek key. », rien n'est enregistré. Sinon « Checking key... », puis
`Brain.CloudModels(key)` en arrière-plan. En cas de succès : modèle par défaut
(`DefaultCloudModel`) ; aucun modèle → « That key can't use any chat models. » ; sinon 🍎
`Keys.Save`, palier fixé par le type de clé (une clé NVIDIA collée en MAX passe en HIGHER), modèle
enregistré dans `cross.json`, liste des modèles affichée, elle salue. Échec réseau → « Couldn't
reach <fournisseur>. Check your internet. » ; erreur du fournisseur → message court
(`Brain.Brief`, tronqué à 60 caractères). Rien n'est enregistré en cas d'échec.

**Forget.** Le bouton affiche Forget quand une clé est enregistrée et le champ vide. Clic : réponse
en cours annulée, 🍎 `Keys.Forget`, modèle du palier vidé, retour au palier NORMAL.

**Changer de palier.** Réponse en cours annulée, palier enregistré, cerveau rechargé
(`TierCloud`). Palier sans clé : invite à coller la clé, le cerveau local reste utilisé.

**Palier NORMAL.** À l'ouverture du palier et sur Refresh, interroge Ollama
(`Brain.InstalledModels`). Clic sur un modèle → `PickBrain`.

**Démarrage.** 🍎 Migration `voice-key`, puis lecture des clés du palier actif dans le Trousseau.
Accès refusé → cerveau local et note « Couldn't read your key from Keychain. ».

**En conversation.** Erreurs cloud gérées par les messages existants (`Brain.Busy`, `Down`, `Gone`).
Pas de bascule automatique vers le local.

## Tests (`--smoke`, `cross/Smoke.cs`)

Faux serveurs locaux comme `FakeVoice()` : `Brain.Nvidia`, `Brain.OpenAi` et `Brain.Ollama`
pointent vers `127.0.0.1:<port>`. Aucun appel réseau réel.

1. **Trousseau** : Save → Read renvoie la clé → Forget → Read renvoie `null`. Service `capri-smoke`,
   nettoyé à la fin. Linux : fichier en `600`.
2. **Migration** : un faux `voice-key` passe dans le Trousseau puis est supprimé ; si l'écriture
   échoue, le fichier reste.
3. **Mainframe** : s'ouvre depuis le clic droit, 3 paliers, `TiersFit` vrai, capture `mainframe.png`.
4. **Connect** : fausse clé NVIDIA → palier HIGHER, modèle par défaut choisi, clé dans le Trousseau,
   clé absente du contenu de `cross.json`. Clé au format inconnu → message, rien enregistré. Faux
   serveur en 401 → message, rien enregistré.
5. **Forget** : bouton Forget, clé retirée du Trousseau, palier NORMAL.
6. **NORMAL** : faux Ollama avec 2 modèles → listés, clic en choisit un. Sans Ollama → note
   « ollama pull gemma4:e2b ».
7. **Menu Brain** : modèles Ollama en NORMAL, modèles cloud en HIGHER.
8. Les tests voix existants passent toujours (clé ElevenLabs via `Keys`).

**Test manuel sur le M2, en fin d'étape** : première demande d'accès du Trousseau par macOS, vraie
clé NVIDIA gratuite, vrai `gemma4:e2b` dans Ollama.
