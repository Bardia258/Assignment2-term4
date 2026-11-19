import SwiftUI

struct LoginView: View {
    @StateObject private var viewModel = AuthViewModel()
    @EnvironmentObject private var session: AuthSession

    private enum Field { case email, password }
    @FocusState private var focusedField: Field?

    var body: some View {
        NavigationStack {
            // ScrollView so the form stays reachable in landscape and with the keyboard up.
            ScrollView {
                VStack(spacing: 20) {
                    Image(systemName: "building.columns.fill")
                        .font(.system(size: 60))
                        .foregroundStyle(.blue)
                        .padding(.top, 40)
                        .accessibilityHidden(true)

                    Text("Welcome Back")
                        .font(.largeTitle)
                        .fontWeight(.bold)

                    VStack(spacing: 15) {
                        TextField("Email", text: $viewModel.email)
                            .keyboardType(.emailAddress)
                            .textContentType(.username)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .submitLabel(.next)
                            .focused($focusedField, equals: .email)
                            .onSubmit { focusedField = .password }
                            .padding()
                            .background(Color(.systemGray6))
                            .clipShape(RoundedRectangle(cornerRadius: 10))

                        SecureField("Password", text: $viewModel.password)
                            .textContentType(.password)
                            .submitLabel(.go)
                            .focused($focusedField, equals: .password)
                            .onSubmit(submit)
                            .padding()
                            .background(Color(.systemGray6))
                            .clipShape(RoundedRectangle(cornerRadius: 10))
                    }
                    .padding(.horizontal)

                    if let error = viewModel.errorMessage {
                        Text(error)
                            .foregroundStyle(.red)
                            .font(.caption)
                            .multilineTextAlignment(.center)
                            .padding(.horizontal)
                    }

                    Button(action: submit) {
                        Group {
                            if viewModel.isLoading {
                                ProgressView().tint(.white)
                            } else {
                                Text("Login").fontWeight(.bold)
                            }
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .padding(.horizontal)
                    .disabled(!viewModel.isLoginValid || viewModel.isLoading)

                    NavigationLink(destination: RegisterView()) {
                        Text("Don't have an account? Register here.")
                            .font(.footnote)
                    }
                }
                .frame(maxWidth: 500)
                .frame(maxWidth: .infinity)
            }
            .scrollDismissesKeyboard(.interactively)
            .toolbar(.hidden, for: .navigationBar)
        }
    }

    private func submit() {
        guard viewModel.isLoginValid, !viewModel.isLoading else { return }
        focusedField = nil
        Task { await viewModel.login(session: session) }
    }
}

#Preview {
    LoginView()
        .environmentObject(AuthSession())
}
