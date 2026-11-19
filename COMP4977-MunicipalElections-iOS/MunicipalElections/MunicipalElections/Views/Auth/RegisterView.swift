import SwiftUI

struct RegisterView: View {
    @StateObject private var viewModel = AuthViewModel()
    @AppStorage("authToken") private var authToken = ""
    @AppStorage("userEmail") private var userEmail = ""

    private enum Field { case name, email, password }
    @FocusState private var focusedField: Field?

    var body: some View {
        ScrollView {
            VStack(spacing: 20) {
                Text("Create Account")
                    .font(.largeTitle)
                    .fontWeight(.bold)
                    .padding(.top, 40)

                VStack(spacing: 15) {
                    TextField("Full Name", text: $viewModel.name)
                        .textContentType(.name)
                        .textInputAutocapitalization(.words)
                        .submitLabel(.next)
                        .focused($focusedField, equals: .name)
                        .onSubmit { focusedField = .email }
                        .padding()
                        .background(Color(.systemGray6))
                        .clipShape(RoundedRectangle(cornerRadius: 10))

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
                        .textContentType(.newPassword)
                        .submitLabel(.join)
                        .focused($focusedField, equals: .password)
                        .onSubmit(submit)
                        .padding()
                        .background(Color(.systemGray6))
                        .clipShape(RoundedRectangle(cornerRadius: 10))
                }
                .padding(.horizontal)
                .disabled(viewModel.isLoading || viewModel.accountCreated)

                // Live feedback so the user isn't surprised by a server-side password error.
                Group {
                    if !viewModel.email.isEmpty && !viewModel.isEmailValid {
                        Text("Enter a valid email address.").foregroundStyle(.red)
                    }
                    if viewModel.password.isEmpty {
                        Text("Use at least 6 characters, including uppercase, lowercase, a number, and a symbol.")
                            .foregroundStyle(.secondary)
                    } else if let requirement = viewModel.passwordRequirementMessage {
                        Text(requirement).foregroundStyle(.orange)
                    }
                }
                .font(.caption)
                .multilineTextAlignment(.center)
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
                            Text(viewModel.accountCreated ? "Finish Signing In" : "Register")
                                .fontWeight(.bold)
                        }
                    }
                    .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .tint(.green)
                .padding(.horizontal)
                .disabled(!canSubmit)
            }
            .frame(maxWidth: 500)
            .frame(maxWidth: .infinity)
        }
        .scrollDismissesKeyboard(.interactively)
        .navigationBarTitleDisplayMode(.inline)
    }

    private var canSubmit: Bool {
        (viewModel.accountCreated || viewModel.isRegisterValid) && !viewModel.isLoading
    }

    private func submit() {
        guard canSubmit else { return }
        focusedField = nil
        Task {
            if let token = await viewModel.register() {
                userEmail = viewModel.trimmedEmail
                authToken = token
            }
        }
    }
}

#Preview {
    NavigationStack { RegisterView() }
}
